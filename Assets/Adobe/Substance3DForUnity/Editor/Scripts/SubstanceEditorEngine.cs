using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

using Adobe.Substance.Input;
using Adobe.SubstanceEditor.Importer;
using Adobe.SubstanceEditor.ProjectSettings;
using Adobe.Substance;
using UnityEditor.SceneManagement;
using UnityEditor.Graphs;

namespace Adobe.SubstanceEditor
{
    /// <summary>
    /// Editor Singleton to manage interactions with the Substance engine.
    /// </summary>
    internal sealed class SubstanceEditorEngine : ScriptableSingleton<SubstanceEditorEngine>
    {
        /// <summary>
        /// Substance files currently loaded in the engine.
        /// </summary>
        private readonly Dictionary<string, SubstanceNativeGraph> _activeSubstanceDictionary = new Dictionary<string, SubstanceNativeGraph>();

        /// <summary>
        /// Currently active instances.
        /// </summary>
        private readonly List<SubstanceGraphSO> _managedInstances = new List<SubstanceGraphSO>();

        private readonly Queue<string> _delayiedInitilization = new Queue<string>();

        /// <summary>
        /// Render results generated by the substance engine in a background thread.
        /// </summary>
        private readonly ConcurrentQueue<RenderResult> _renderResultsQueue = new ConcurrentQueue<RenderResult>();

        private readonly List<SubstanceGraphSO> _playmodeObjects = new List<SubstanceGraphSO>();

        private readonly Queue<DelayAssetCreationInfo> _creationQueue = new Queue<DelayAssetCreationInfo>();

        private class DelayAssetCreationInfo
        {
            public SubstanceGraphSO InstanceAsset;
            public string InstancePath;

            public DelayAssetCreationInfo(SubstanceGraphSO instanceAsset, string instancePath)
            {
                InstanceAsset = instanceAsset;
                InstancePath = instancePath;
            }
        }

        internal void DelayAssetCreation(SubstanceGraphSO instanceAsset, string instancePath)
        {
            _creationQueue.Enqueue(new DelayAssetCreationInfo(instanceAsset, instancePath));
        }

        private bool _resetAllInputs = false;

        /// <summary>
        /// Initializer to ensure SubstanceEditorEngine is
        /// started consistently on editor load and assembly reload.
        ///</summary>
        [InitializeOnLoad]
        private sealed class SubstanceEditorEngineInitializer
        {
            static SubstanceEditorEngineInitializer()
            {
                AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
                AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            }

            private static void OnBeforeAssemblyReload()
            {
                SubstanceEditorEngine.instance.TearDown();
            }

            private static void OnAfterAssemblyReload()
            {
                SubstanceEditorEngine.instance.Setup();
            }
        }

        private static Queue<Tuple<string, string>> _delayMoveOperation = new Queue<Tuple<string, string>>();

        internal void PushMoveOperation(string from, string to)
        {
            _delayMoveOperation.Enqueue(new Tuple<string, string>(from, to));
        }

        private bool _isLoaded;

        public bool IsInitialized => _isLoaded;

        /// <summary>
        /// Prepares infrastructure for managing substance assets in the editor. 
        /// Intializes the substance engine and starts listening for Editor events.
        /// </summary>
        private void Setup()
        {
            _isLoaded = false;
            PluginPipelines.GetCurrentPipelineInUse();
            var enginePath = PlatformUtils.GetEnginePath();
            var pluginPath = PlatformUtils.GetPluginPath();
            Engine.Initialize(pluginPath, enginePath);
            EditorApplication.update += Update;
            EditorApplication.quitting += OnQuit;
            EditorApplication.playModeStateChanged += PlaymodeStateChanged;
            Undo.undoRedoPerformed += UndoCallback;
        }
        
        private void UndoCallback()
        {
            if (Selection.activeObject is SubstanceGraphSO)
            {
                var target = Selection.activeObject as SubstanceGraphSO;

                var managedInstance = _managedInstances.FirstOrDefault((a) => a.GUID == target.GUID);

                if (managedInstance != null)
                {
                    managedInstance.RenderTextures = true;
                    PushAllInputsToUpdate();
                }
            }
        }

        /// <summary>
        /// Stops listening to Editor events and shuts down the substance engine.
        /// </summary>
        private void TearDown()
        {
            _isLoaded = false;
            EditorApplication.update -= Update;
            EditorApplication.quitting -= OnQuit;
            Undo.undoRedoPerformed -= UndoCallback;
            EditorApplication.playModeStateChanged += PlaymodeStateChanged;
            Engine.Shutdown();
        }

        private void OnQuit()
        {
            //Check if there are pending updates. If there is, we need to do them synchronously.
            foreach (var graph in _managedInstances)
            {
                if (graph == null)
                    continue;

                if (!TryGetHandlerFromInstance(graph, out SubstanceNativeGraph substanceHandler))
                    continue;

                if (substanceHandler.InRenderWork)
                    continue;

                if (graph.RenderTextures)
                {
                    var renderResult = substanceHandler.Render();
                    graph.UpdateAssociatedAssets(renderResult, true);
                }
            }
        }

        #region Update

        /// <summary>
        /// Editor update.
        /// </summary>
        private void Update()
        {
            if (!_isLoaded)
            {
                LoadAllRuntimeOnlySbsarFiles();
                _isLoaded = true;
            }

            _managedInstances.RemoveAll(item => item == null);

            CheckDelayedMove();
            HandleAssetCreation();
            HandleDelayedInitialization();
            CheckUIUpdate();
            CheckRenderResultsUpdates();
        }

        private void CheckDelayedMove()
        {
            while (_delayMoveOperation.Count != 0)
            {
                var newMove = _delayMoveOperation.Dequeue();

                var substanceInstance = AssetDatabase.LoadAssetAtPath<SubstanceGraphSO>(newMove.Item2);

                if (substanceInstance != null)
                {
                    substanceInstance.Move(newMove.Item2);
                }
            }
        }

        private void HandleAssetCreation()
        {
            while (_creationQueue.Count != 0)
            {
                var creationData = _creationQueue.Dequeue();

                if (creationData == null)
                    continue;

                if (creationData.InstanceAsset == null)
                    continue;

                var oldAsset = AssetDatabase.LoadAssetAtPath<SubstanceGraphSO>(creationData.InstancePath);

                AssetDatabase.CreateAsset(creationData.InstanceAsset, creationData.InstancePath);

                var createAsset = AssetDatabase.LoadAssetAtPath<SubstanceGraphSO>(creationData.InstancePath);
                var importer = AssetImporter.GetAtPath(createAsset.AssetPath) as SubstanceImporter;
                EditorUtility.SetDirty(importer);
                AssetDatabase.Refresh();
                SubmitAsyncRenderWorkBatch(new List<SubstanceGraphSO> { creationData.InstanceAsset });
            }
        }

        private void HandleDelayedInitialization()
        {
            while (_delayiedInitilization.Count != 0)
            {
                var instancePath = _delayiedInitilization.Dequeue();
                var materialInstance = AssetDatabase.LoadAssetAtPath<SubstanceGraphSO>(instancePath);
                _managedInstances.Add(materialInstance);
            }
        }


        #region Playmode

        //Callback
        private void PlaymodeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                HandlePlaymodeEnter();
            }

            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                HandlePlaymodeExit();
            }
        }

        private static T[] GetAllInstances<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);  //FindAssets uses tags check documentation for more info
            T[] a = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)         //probably could get optimized 
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                a[i] = AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return a;
        }

        private void HandlePlaymodeEnter()
        {
            var runtimeGraphcsBehavior = FindObjectsOfType<Substance.Runtime.SubstanceRuntimeGraph>();

            foreach (var material in runtimeGraphcsBehavior)
            {
                if (material.GraphSO != null && Application.IsPlaying(material))
                {
                    if (!_playmodeObjects.Contains(material.GraphSO))
                        _playmodeObjects.Add(material.GraphSO);
                }
            }

            var runtimeOnlyGraphs = GetAllInstances<SubstanceGraphSO>().Where(a => a.IsRuntimeOnly);

            foreach (var runtimeOnlyGraph in runtimeOnlyGraphs)
            {
                if (!_playmodeObjects.Contains(runtimeOnlyGraph))
                    _playmodeObjects.Add(runtimeOnlyGraph);
            }

        }

        private void HandlePlaymodeExit()
        {
            foreach (var playmodeObject in _playmodeObjects)
            {
                playmodeObject.RenderTextures = true;
            }

            _playmodeObjects.Clear();
        }

        #endregion Playmode

        /// <summary>
        /// Updated the state of the SubstanceFileHandlers based on changes made in the graph objects.
        /// </summary>
        private void CheckUIUpdate()
        {
            foreach (var graph in _managedInstances)
            {
                if (graph == null)
                    continue;

                if (graph.RawData == null)
                {
                    var assets = AssetDatabase.LoadAllAssetsAtPath(graph.AssetPath);

                    if (assets == null)
                        continue;

                    var dataObject = assets.FirstOrDefault(a => a is SubstanceFileRawData) as SubstanceFileRawData;

                    graph.RawData = dataObject;
                    EditorUtility.SetDirty(graph);
                    AssetDatabase.Refresh();
                }

                if (!TryGetHandlerFromInstance(graph, out SubstanceNativeGraph substanceHandler))
                    continue;

                if (substanceHandler.InRenderWork)
                    continue;

                if (graph.IsRuntimeOnly && graph.OutputMaterial != null)
                    if (graph.OutputMaterial.GetTexture("_MainTex") == null)
                        MaterialUtils.AssignOutputTexturesToMaterial(graph);

                if (HasMaterialShaderChanged(graph))
                {
                    SubmitAsyncRenderWork(substanceHandler, graph, true);
                    graph.RenderTextures = true;
                    continue;
                }

                if (graph.OutputRemaped)
                {
                    graph.OutputRemaped = false;

                    if (graph.IsRuntimeOnly)
                    {
                        DeleteGeneratedTextures(graph);
                    }

                    RenderingUtils.UpdateAlphaChannelsAssignment(substanceHandler, graph);
                    SubmitAsyncRenderWork(substanceHandler, graph, true);
                    graph.RenderTextures = true;
                    continue;
                }

                if (graph.RenderTextures)
                {
                    graph.RenderTextures = false;

                    if (_resetAllInputs)
                    {
                        _resetAllInputs = true;

                        foreach (var input in graph.Input)
                        {
                            input.UpdateNativeHandle(substanceHandler);
                        }
                    }

                    SubmitAsyncRenderWork(substanceHandler, graph);

                    EditorUtility.SetDirty(graph);
                }
            }
        }

        /// <summary>
        /// Updated the render results that are finished by the substance engine
        /// </summary>
        private void CheckRenderResultsUpdates()
        {
            if (_renderResultsQueue.TryDequeue(out RenderResult renderResult))
            {
                SubstanceGraphSO graph = _managedInstances.FirstOrDefault(a => a.GUID == renderResult.GUID);

                if (graph == null)
                    return;

                if (!TryGetHandlerFromInstance(graph, out SubstanceNativeGraph handler))
                    return;

                var textureReassigned = graph.UpdateAssociatedAssets(renderResult.Result, renderResult.ForceRebuild);

                if (textureReassigned)
                {
                    if (!string.IsNullOrEmpty(graph.AssetPath))
                    {
                        AssetCreationUtils.CreateMaterialOrUpdateMaterial(graph, graph.Name);
                        EditorUtility.SetDirty(graph);
                        EditorUtility.SetDirty(graph.OutputMaterial);
                        AssetDatabase.Refresh();
                    }
                }
                else
                {
                    if (graph.OutputMaterial == null)
                    {
                        AssetCreationUtils.CreateMaterialOrUpdateMaterial(graph, graph.Name);
                        EditorUtility.SetDirty(graph);
                        EditorUtility.SetDirty(graph.OutputMaterial);
                        AssetDatabase.Refresh();
                    }
                    else
                    {
                        EditorUtility.SetDirty(graph.OutputMaterial);
                    }
                }

                handler.InRenderWork = false;
            }
        }

        /// <summary>
        /// Checks if the shaders assigned to the substance graph generated material has changed. If so, we have to change the default outputs.
        /// </summary>
        private bool HasMaterialShaderChanged(SubstanceGraphSO graph)
        {
            if (graph.OutputMaterial == null || string.IsNullOrEmpty(graph.MaterialShader))
                return false;

            if (graph.OutputMaterial.shader.name == graph.MaterialShader)
                return false;

            AssetCreationUtils.UpdateMeterialAssignment(graph);
            return true;
        }

        #endregion Update

        #region Public methods

        #region Instance Management

        /// <summary>
        /// Loads a sbsar file into the engine. The engine will keep track of this file internally.
        /// </summary>
        /// <param name="assetPath">Path to a sbsar file.</param>
        public void InitializeInstance(SubstanceGraphSO substanceInstance, string instancePath, out SubstanceGraphSO matchingInstance)
        {
            matchingInstance = null;

            if (substanceInstance == null)
                return;

            if (string.IsNullOrEmpty(substanceInstance.AssetPath))
                Debug.LogError("Unable to instantiate substance material with null assetPath.");

            matchingInstance = _managedInstances.FirstOrDefault(a => a.OutputPath().Equals(substanceInstance.OutputPath(), StringComparison.OrdinalIgnoreCase));

            if (!_activeSubstanceDictionary.TryGetValue(substanceInstance.GUID, out SubstanceNativeGraph _))
            {
                var substanceArchive = Engine.OpenFile(substanceInstance.RawData.FileContent, substanceInstance.GetNativeID());
                _activeSubstanceDictionary.Add(substanceInstance.GUID, substanceArchive);
            }

            if (!string.IsNullOrEmpty(instancePath))
                _delayiedInitilization.Enqueue(instancePath);
            else
            {
                _managedInstances.Add(substanceInstance);
            }
        }

        /// <summary>
        /// Unloads the target substance from th e substance engine.
        /// </summary>
        /// <param name="assetPath">Path to a sbsar file.</param>
        public void ReleaseInstance(SubstanceGraphSO substanceInstance)
        {
            if (TryGetHandlerFromInstance(substanceInstance, out SubstanceNativeGraph substanceArchive))
            {
                _activeSubstanceDictionary.Remove(substanceInstance.GUID);
                substanceArchive.Dispose();

                var managedInstance = _managedInstances.FirstOrDefault((a) => a.GUID == substanceInstance.GUID);

                if (managedInstance != null)
                    _managedInstances.Remove(managedInstance);
            }
        }

        public string SerializeCurrentState(SubstanceGraphSO substanceInstance)
        {
            if (TryGetHandlerFromInstance(substanceInstance, out SubstanceNativeGraph substanceArchive))
                return substanceArchive.CreatePresetFromCurrentState();

            return string.Empty;
        }

        public void SetStateFromSerializedData(SubstanceGraphSO substanceInstance, string data)
        {
            if (TryGetHandlerFromInstance(substanceInstance, out SubstanceNativeGraph substanceArchive))
                substanceArchive.ApplyPreset(data);
        }

        #endregion Instance Management

        public void PushAllInputsToUpdate()
        {
            _resetAllInputs = true;
        }

        /// <summary>
        /// Loads the list of substance graphs from a substance file.
        /// </summary>
        /// <param name="assetPath">Path to the target substance file.</param>
        /// <returns>List of substance graph objects.</returns>
        public void CreateGraphObject(SubstanceGraphSO instance, SubstanceGraphSO copy = null)
        {
            if (!TryGetHandlerFromInstance(instance, out SubstanceNativeGraph substanceHandle))
                return;

            if (copy != null)
            {
                if (TryGetHandlerFromInstance(copy, out SubstanceNativeGraph copyHandle))
                {
                    var copyPreset = copyHandle.CreatePresetFromCurrentState();
                    substanceHandle.ApplyPreset(copyPreset); ;
                }
            }

            instance.Input = GetGraphInputs(substanceHandle);
            instance.Output = GetGraphOutputs(substanceHandle);
            instance.PhysicalSize = substanceHandle.GetPhysicalSize();
            instance.HasPhysicalSize = instance.PhysicalSize != Vector3.zero;

            RenderingUtils.ConfigureOutputTextures(substanceHandle, instance);

            instance.GenerateAllOutputs = SubstanceEditorSettingsSO.GenerateAllTextures();
            SetOutputTextureSize(instance, substanceHandle);

            instance.DefaultPreset = substanceHandle.CreatePresetFromCurrentState();

            var thumbnailData = substanceHandle.GetThumbnail();

            if (thumbnailData != null)
            {
                instance.Thumbnail = thumbnailData;
                instance.HasThumbnail = true;
            }
        }

        /// <summary>
        /// Renders a SubstanceGraphSO asynchronously.
        /// </summary>
        /// <param name="instances">Target SubstanceGraphSO to render.</param>
        public void RenderInstanceAsync(SubstanceGraphSO instances)
        {
            if (TryGetHandlerFromInstance(instances, out SubstanceNativeGraph substanceArchive))
                SubmitAsyncRenderWork(substanceArchive, instances);
        }

        /// <summary>
        /// Renders multiple SubstanceGraphSO asynchronously.
        /// </summary>
        /// <param name="instances">List of target SubstanceGraphSO objects to render.</param>
        public void RenderInstanceAsync(IReadOnlyList<SubstanceGraphSO> instances)
        {
            foreach (var graph in instances)
                RenderInstanceAsync(graph);
        }

        #region Preset

        /// <summary>
        /// Get the preset XML document for the current state of the a managed substance object.
        /// </summary>
        /// <param name="assetPath">Path to the target sbsar file.</param>
        /// <param name="graphID">Target graph id. </param>
        /// <returns>XML document with the current input states as a preset. </returns>
        public string ExportGraphPresetXML(SubstanceGraphSO instance)
        {
            if (!TryGetHandlerFromInstance(instance, out SubstanceNativeGraph substanceArchive))
                return null;

            return substanceArchive.CreatePresetFromCurrentState();
        }

        /// <summary>
        /// Loads the inputs from a preset XML document into the target graph of a managed substance file.
        /// </summary>
        /// <param name="substanceInstancePath">Path to the target sbsar file.</param>
        /// <param name="graphID">Target graph id.</param>
        /// <param name="presetXML">Preset XML document.</param>
        public void LoadPresetsToGraph(SubstanceGraphSO instance, string presetXML)
        {
            if (TryGetHandlerFromInstance(instance, out SubstanceNativeGraph substanceHandler))
            {
                substanceHandler.ApplyPreset(presetXML);

                instance.Input = GetGraphInputs(substanceHandler);
                instance.RenderTextures = true;
                EditorUtility.SetDirty(instance);
            }
        }

        /// <summary>
        /// Loads the inputs from a preset XML document into the target graph of a managed substance file.
        /// </summary>
        /// <param name="substanceInstancePath">Path to the target sbsar file.</param>
        /// <param name="graphID">Target graph id.</param>
        /// <param name="presetXML">Preset XML document.</param>
        public void LoadBakedPresetsToGraph(SubstanceGraphSO instance, int index)
        {
            if (TryGetHandlerFromInstance(instance, out SubstanceNativeGraph substanceHandler))
            {
                substanceHandler.ApplyBakedPreset(index);
                SetOutputTextureSize(instance, substanceHandler);

                instance.Input = GetGraphInputs(substanceHandler);
                instance.RenderTextures = true;
                EditorUtility.SetDirty(instance);
            }
        }

        #endregion Preset

        #endregion Public methods

        public bool TryGetHandlerFromInstance(SubstanceGraphSO substanceInstance, out SubstanceNativeGraph substanceHandler)
        {
            substanceHandler = null;

            if (substanceInstance == null)
                return false;

            if (!_activeSubstanceDictionary.TryGetValue(substanceInstance.GUID, out substanceHandler))
            {
                return false;
            }

            return true;
        }

        public bool TryGetHandlerFromGUI(string guid, out SubstanceNativeGraph substanceHandler)
        {
            if (!_activeSubstanceDictionary.TryGetValue(guid, out substanceHandler))
                return false;

            return true;
        }

        public void UpdateGraphsToNewFile(string assetPath, byte[] fileContent)
        {
            var updatedGraphs = new List<SubstanceGraphSO>();

            foreach (var graph in _managedInstances)
            {
                if (graph.AssetPath == assetPath)
                {
                    if(TryGetHandlerFromInstance(graph, out SubstanceNativeGraph nativeGraph))
                    {
                        var currentState = nativeGraph.CreatePresetFromCurrentState();
                        nativeGraph.Dispose();
                        _activeSubstanceDictionary[graph.GUID] = Engine.OpenFile(fileContent, graph.GetNativeID());
                        _activeSubstanceDictionary[graph.GUID].ApplyPreset(currentState);

                        graph.Input = GetGraphInputs(_activeSubstanceDictionary[graph.GUID]);
                        graph.Output = GetGraphOutputs(_activeSubstanceDictionary[graph.GUID]);

                        RenderingUtils.ConfigureOutputTextures(_activeSubstanceDictionary[graph.GUID], graph);

                        EditorUtility.SetDirty(graph);

                        updatedGraphs.Add(graph);
                    }
                }
            }

            SubmitAsyncRenderWorkBatch(updatedGraphs);
        }

        /// <summary>
        /// Loads all sbsar files currently in the project marked as "Runtime only".
        /// </summary>
        private void LoadAllRuntimeOnlySbsarFiles()
        {
            string[] files = Directory.GetFiles(Application.dataPath, "*.sbsar", SearchOption.AllDirectories);

            foreach (string filePath in files)
            {
                if (filePath.StartsWith(Application.dataPath))
                {
                    var assetPath = "Assets" + filePath.Substring(Application.dataPath.Length);
                    assetPath = assetPath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (!File.Exists(assetPath))
                        continue;

                    SubstanceImporter importer = AssetImporter.GetAtPath(assetPath) as SubstanceImporter;

                    if (importer == null)
                        continue;

                    foreach (var substanceInstance in importer._fileAsset.GetGraphs())
                    {
                        if (!substanceInstance.IsRuntimeOnly)
                            return;

                        InitializeInstance(substanceInstance, AssetDatabase.GetAssetPath(substanceInstance), out SubstanceGraphSO _);

                        if (TryGetHandlerFromInstance(substanceInstance, out SubstanceNativeGraph fileHandler))
                        {
                            if (substanceInstance == null)
                                continue;

                            substanceInstance.RuntimeInitialize(fileHandler, substanceInstance.IsRuntimeOnly);
                        }
                    }
                }
            }
        }

        public void RefreshActiveInstances()
        {
            foreach (var substanceInstance in _managedInstances)
            {
                substanceInstance.RenderTextures = true;
            }
        }

        private List<ISubstanceInput> GetGraphInputs(SubstanceNativeGraph substanceFileHandler)
        {
            var inputs = new List<ISubstanceInput>();

            var graphInputCount = substanceFileHandler.GetInputCount();

            for (int j = 0; j < graphInputCount; j++)
            {
                SubstanceInputBase graphInput = substanceFileHandler.GetInputObject(j);
                inputs.Add(graphInput);
            }

            return inputs;
        }

        private List<SubstanceOutputTexture> GetGraphOutputs(SubstanceNativeGraph substanceFileHandler)
        {
            var outputs = new List<SubstanceOutputTexture>();

            var graphOutputCount = substanceFileHandler.GetOutputCount();

            for (int j = 0; j < graphOutputCount; j++)
            {
                var outputDescription = substanceFileHandler.GetOutputDescription(j);
                var unityTextureName = MaterialUtils.GetUnityTextureName(outputDescription);
                SubstanceOutputTexture graphData = new SubstanceOutputTexture(outputDescription, unityTextureName);

                if (graphData.IsBaseColor() ||
                    graphData.IsDiffuse() ||
                    graphData.IsSpecular() ||
                    graphData.IsHightMap() ||
                    graphData.IsEmissive())
                {
                    graphData.sRGB = true;
                }
                else
                {
                    graphData.sRGB = false;
                }

                outputs.Add(graphData);
            }

            return outputs;
        }

        private void SetOutputTextureSize(SubstanceGraphSO graph, SubstanceNativeGraph substanceFileHandler)
        {
            var outputSize = graph.Input.FirstOrDefault(a => a.Description.Label == "$outputsize");

            if (outputSize == null)
                return;

            if (outputSize is SubstanceInputInt2 outputSizeInput)
            {
                outputSizeInput.Data = SubstanceEditorSettingsSO.TextureOutputResultion();
                outputSizeInput.UpdateNativeHandle(substanceFileHandler);
            }
        }

        #region Rendering

        public void SubmitAsyncRenderWork(SubstanceNativeGraph substanceArchive, SubstanceGraphSO instanceKey, bool forceRebuild = false)
        {
            if (substanceArchive.InRenderWork)
                return;

            substanceArchive.InRenderWork = true;

            var renderResut = new RenderResult()
            {
                SubstanceArchive = substanceArchive,
                ForceRebuild = forceRebuild,
                GUID = instanceKey.GUID
            };

            Task.Run(() =>
            {
                try
                {
                    renderResut.Result = substanceArchive.Render();
                    _renderResultsQueue.Enqueue(renderResut);
                }
                catch (Exception e)
                {
                    substanceArchive.InRenderWork = false;
                    Debug.LogException(e);
                }
            });
        }

        public void SubmitAsyncRenderWorkBatch(IReadOnlyList<SubstanceGraphSO> instanceKey)
        {
            var guildList = instanceKey.Select(a => a.GUID).ToArray();

            Task.Run(() =>
            {
                foreach (var guid in guildList)
                {
                    if (!TryGetHandlerFromGUI(guid, out SubstanceNativeGraph substanceArchive))
                        continue;

                    if (substanceArchive.InRenderWork)
                        continue;

                    substanceArchive.InRenderWork = true;

                    var renderResut = new RenderResult()
                    {
                        SubstanceArchive = substanceArchive,
                        ForceRebuild = false,
                        GUID = guid
                    };

                    try
                    {
                        renderResut.Result = substanceArchive.Render();
                        _renderResultsQueue.Enqueue(renderResut);
                    }
                    catch (Exception e)
                    {
                        substanceArchive.InRenderWork = false;
                        Debug.LogException(e);
                    }
                }
            });
        }

        private void DeleteGeneratedTextures(SubstanceGraphSO graph)
        {
            foreach (var output in graph.Output)
            {
                if (output.OutputTexture != null)
                {
                    var texturePath = AssetDatabase.GetAssetPath(output.OutputTexture);

                    if (!string.IsNullOrEmpty(texturePath))
                        AssetDatabase.DeleteAsset(texturePath);
                }
            }
        }

        private struct RenderResult
        {
            public SubstanceNativeGraph SubstanceArchive;
            public IntPtr Result;
            public bool ForceRebuild;
            public string GUID;
        }

        #endregion Rendering
    }
}