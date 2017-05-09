﻿using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.MemoryProfiler;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using System.IO;
namespace GoProfiler
{
    public enum ClassIDMap : int
	{
		AnimationClip = 74,
		Animator = 95,
		AnimatorController = 91,
		AssetBundle = 142,
		AudioClip = 83,
		AudioManager = 11,
		AudioSource = 82,
		BoxCollider = 65,
		Camera = 20,
		CubeMap = 89,
		Font = 128,
		GameObject = 1,
		LineRenderer = 120,
		Material = 21,
        Mesh = 43,
		MeshRenderer = 23,
		MonoBehavior = 114,
		MonoScript = 115,
		ParticleSystem = 198,
		ParticleSystemRenderer = 199,
		RenderTexture = 84,
		ResourceManager = 12,
		Rigidbody = 54,
		ScriptMapper=94,
		Shader = 48,
		SkinnedMeshRenderer = 137,
		TextAsset = 49,
		TextMesh = 102,
		Texture2D = 28,
		Transform = 4,
        MeshFilter = 33,
    }
    [Serializable]
    public class PackedMemoryData
    {
        [SerializeField]
		public PackedMemorySnapshot mSnapshot;
    }
    public class GoProfilerWindow : EditorWindow
    {
        [NonSerialized]
        PackedItemNode memoryRootNode;
        [SerializeField]
        PackedMemoryData data;
        [SerializeField]
        MemoryFilterSettings memoryFilters;
        protected Vector2 scrollPosition = Vector2.zero;
        int _prevInstance;
        int selectedIndex;
        GUIStyle toolBarStyle;
        //bool showObjectInspector = false;
        [SerializeField]
        bool canSaveDetails = false;
        [SerializeField]
        int savedMinSize = 0;
        [NonSerialized]
        public static PackedItemNode selectedObject;
        [NonSerialized]
        UnityEngine.Object objectField;
        DateTime lastRefreshTime;
        [MenuItem("Window/Go-Profiler")]
        static void ShowWindow()
        {
            GoProfilerWindow window = (GetWindow(typeof(GoProfilerWindow)) as GoProfilerWindow);
            window.titleContent = new GUIContent("Go Profiler", AssetPreview.GetMiniTypeThumbnail(typeof(UnityEngine.EventSystems.EventSystem)), "Amazing!");
            window.Init();
        }
        void Init()
        {
			if (data == null)
				data = new PackedMemoryData();

			if(!memoryFilters)
				memoryFilters = AssetDatabase.LoadAssetAtPath<MemoryFilterSettings>("Assets/GoProfiler/Editor/MemoryFilters.asset");

			if (toolBarStyle == null) {
				toolBarStyle = new GUIStyle();
				toolBarStyle.alignment = TextAnchor.MiddleCenter;
				toolBarStyle.normal.textColor = Color.white;
				toolBarStyle.fontStyle = FontStyle.Bold;
            }
            if (memoryRootNode == null)
            {
                Debug.Log("Go-Profiler Init");
                memoryRootNode = new PackedItemNode("Root");
                IncomingSnapshot(data.mSnapshot);
            }
        }
        void OnEnable()
        {
            MemorySnapshot.OnSnapshotReceived += IncomingSnapshot;
			Init ();
        }
        public void OnDisable()
        {
            MemorySnapshot.OnSnapshotReceived -= IncomingSnapshot;
            selectedObject = null;
        }
        public void ClearEditorReferences() {
            EditorUtility.UnloadUnusedAssetsImmediate();
            System.GC.Collect();
            Resources.UnloadUnusedAssets();
        }
        private void OnSelectProfilerClick(object userData, string[] options, int selected)
        {
            selectedIndex = selected;
            int num = this.connectionGuids[selected];
            this.lastOpenedProfiler = ProfilerDriver.GetConnectionIdentifier(num);
            ProfilerDriver.connectedProfiler = num;
        }
        int[] connectionGuids;
        string lastOpenedProfiler;
        void OnGUI()
        {
            Init();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            //string[] array3 = new string[] { "Editor", "Fucker", "Sucker" };

            if (GUILayout.Button(new GUIContent("Active Profler"), EditorStyles.toolbarDropDown))
            {
                Rect titleRect2 = EditorGUILayout.GetControlRect();
                titleRect2.y += EditorStyles.toolbarDropDown.fixedHeight;
                connectionGuids = ProfilerDriver.GetAvailableProfilers();
                GUIContent[] guiContents = new GUIContent[connectionGuids.Length];
                for (int i = 0; i < connectionGuids.Length; i++)
                {
                    if (connectionGuids[i] == ProfilerDriver.connectedProfiler) {
                        selectedIndex = i;
                    }
                    bool flag = ProfilerDriver.IsIdentifierConnectable(connectionGuids[i]);
                    string text = ProfilerDriver.GetConnectionIdentifier(connectionGuids[i]);
                    if (!flag)
                    {
                        text += " (Version mismatch)";//I don't know what this means...
                    }
                    guiContents[i] = new GUIContent(text);
                }
                EditorUtility.DisplayCustomMenu(titleRect2, guiContents, selectedIndex, OnSelectProfilerClick, null);
            }
            //EditorGUILayout.Popup(selectedIndex, array3, EditorStyles.toolbarPopup);

            if (GUILayout.Button("Take Sample: " + ProfilerDriver.GetConnectionIdentifier(ProfilerDriver.connectedProfiler), EditorStyles.toolbarButton))
            {
                //ProfilerDriver.ClearAllFrames();
                //ProfilerDriver.deepProfiling = true;
                ShowNotification(new GUIContent("Waiting for device..."));
                MemorySnapshot.RequestNewSnapshot();
            }
            if (GUILayout.Button(new GUIContent("Clear Editor References", "Design for profile in editor.\nEditorUtility.UnloadUnusedAssetsImmediate() can be called."), EditorStyles.toolbarButton))
            {
                ClearEditorReferences();
            }
            GUILayout.FlexibleSpace();
            //GUILayout.Space(5);
            EditorGUILayout.EndHorizontal();




            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Snapshot", EditorStyles.toolbarButton))
            {
                if (data.mSnapshot != null)
                {
                    string filePath = EditorUtility.SaveFilePanel("Save Snapshot", null, "MemorySnapshot" + System.DateTime.Now.ToString("_MMdd_HHmm"), "memsnap");
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        using (Stream stream = File.Open(filePath, FileMode.Create))
                        {
                            bf.Serialize(stream, data.mSnapshot);
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("No snapshot to save.  Try taking a snapshot first.");
                }
            }
            if (GUILayout.Button("Load Snapshot", EditorStyles.toolbarButton))
            {
                string filePath = EditorUtility.OpenFilePanel("Load Snapshot", null, "memsnap");
                if (!string.IsNullOrEmpty(filePath))
                {
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    using (Stream stream = File.Open(filePath, FileMode.Open))
                    {
                        IncomingSnapshot(bf.Deserialize(stream) as PackedMemorySnapshot);
                    }
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save Snapshot as .txt", EditorStyles.toolbarButton))
            {
                if (memoryRootNode != null)
                {
                    string filePath = EditorUtility.SaveFilePanel("Save Snapshot as .txt", null, "Mindmap" + System.DateTime.Now.ToString("_MMdd_HHmm"), "txt");
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        SaveToMindMap(filePath);
                    }
                }
                else
                {
                    Debug.LogWarning("No snapshot to save.  Try taking a snapshot first.");
                }
            }
            if (GUILayout.Button("Load Snapshot from .txt", EditorStyles.toolbarButton))
            {
                string filePath = EditorUtility.OpenFilePanel("Load Snapshot from .txt", null, "txt");
                if (!string.IsNullOrEmpty(filePath))
                {
                    LoadFromMindMap(filePath);
                }
            }
            canSaveDetails = GUILayout.Toggle(canSaveDetails, "saveDetails");
            savedMinSize = EditorGUILayout.IntField("minSize", savedMinSize, EditorStyles.toolbarTextField);
            if (savedMinSize < 0)
                savedMinSize = 0;
            EditorGUILayout.EndHorizontal();
            //Top tool bar end...


            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            memoryFilters = EditorGUILayout.ObjectField(memoryFilters, typeof(MemoryFilterSettings), false) as MemoryFilterSettings;
            //if (GUILayout.Button(new GUIContent("Save as plist/xml", "TODO in the future..."), EditorStyles.toolbarButton))
            //{
            //}
            //if (GUILayout.Button(new GUIContent("Load plist/xml", "TODO in the future..."), EditorStyles.toolbarButton))
            //{
            //}
            GUILayout.FlexibleSpace();
            GUILayout.Label(new GUIContent("[LastRefreshTime]" + lastRefreshTime.ToShortTimeString(), 
                " enter / exit play mode or change a script,Unity has to reload the mono assemblies , and the GoProfilerWindow has to Refresh immediately"));
            if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Refresh"), EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                IncomingSnapshot(data.mSnapshot);
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            if (!memoryFilters)
            {
                EditorGUILayout.HelpBox("Please Select a MemoryFilters object or load it from the .plist/.xml file", MessageType.Warning);
            }

            //TODO: handle the selected object.
            //EditorGUILayout.HelpBox("Watching Texture Detail Data is only for Editor.", MessageType.Warning, true);
            if (selectedObject != null && selectedObject.childList.Count == 0)
            {
                if (selectedObject != null && _prevInstance != selectedObject.instanceID)
                {
                    objectField = EditorUtility.InstanceIDToObject(selectedObject.instanceID);
                    _prevInstance = selectedObject.instanceID;
                    //Selection.instanceIDs = new int[] { selectedObject.instanceID }; //Hide in inspector.
                }
            }
            if (objectField != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Selected Object Info:");
                EditorGUILayout.ObjectField(objectField, objectField.GetType(), true);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("Can't instance object,maybe it was already released.");
            }
            //MemoryFilters end...

            Rect titleRect = EditorGUILayout.GetControlRect();
            EditorGUI.DrawRect(titleRect, new Color(0.15f, 0.15f, 0.15f, 1));
            EditorGUI.DrawRect(new Rect(titleRect.x + titleRect.width - 200, titleRect.y, 1, Screen.height), new Color(0.15f, 0.15f, 0.15f, 1));
            EditorGUI.DrawRect(new Rect(titleRect.x + titleRect.width - 100, titleRect.y, 1, Screen.height), new Color(0.15f, 0.15f, 0.15f, 1));
            GUI.Label(new Rect(titleRect.x, titleRect.y, titleRect.width - 200, titleRect.height), "Name", toolBarStyle);
            GUI.Label(new Rect(titleRect.x + titleRect.width - 175, titleRect.y, 50, titleRect.height), "Size", toolBarStyle);
            GUI.Label(new Rect(titleRect.x + titleRect.width - 75, titleRect.y, 50, titleRect.height), "RefCount", toolBarStyle);
            //Title bar end...

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            if (memoryRootNode != null && memoryRootNode.childList != null)
                memoryRootNode.DrawGUI(0);
            else
                Init();
            GUILayout.EndScrollView();
            //Contents end...
            //handle the select event to Repaint
            if (Event.current.type == EventType.mouseDown)
            {
                Repaint();
            }
        }
        void IncomingSnapshot(PackedMemorySnapshot snapshot)
        {
            if (null==snapshot)
                return;
            lastRefreshTime = DateTime.Now;
            Debug.Log("GoProfilerWindow.IncomingSnapshot");
			data.mSnapshot = snapshot;
            memoryRootNode = new PackedItemNode("Root");
            PackedItemNode assetsRoot = new PackedItemNode("Assets");
            PackedItemNode sceneMemoryRoot = new PackedItemNode("SceneMemory");
            PackedItemNode notSavedRoot = new PackedItemNode("NotSaved");
            PackedItemNode builtinResourcesRoot = new PackedItemNode("BuiltinResources");
            PackedItemNode unknownRoot = new PackedItemNode("Unknown");
            //PackedItemNode managerNode = PackedItemNode.BuildNode<PackedItemNode>("Managers");
            memoryRootNode.AddNode(assetsRoot);
            memoryRootNode.AddNode(sceneMemoryRoot);
            memoryRootNode.AddNode(notSavedRoot);
            memoryRootNode.AddNode(builtinResourcesRoot);
            //assetsRoot.AddNode(managerNode);
            Dictionary<int, Group> assetGroup = new Dictionary<int, Group>();
            Dictionary<int, Group> sceneMemoryGroup = new Dictionary<int, Group>();
            Dictionary<int, Group> notSavedGroup = new Dictionary<int, Group>();
            Dictionary<int, Group> builtinResourcesGroup = new Dictionary<int, Group>();
            Dictionary<int, Group> unknownGroup = new Dictionary<int, Group>();//I can't find any unknown object yet.
            List<PackedNativeUnityEngineObject> assetsObjectList = new List<PackedNativeUnityEngineObject>();
            List<PackedNativeUnityEngineObject> sceneMemoryObjectList = new List<PackedNativeUnityEngineObject>();
            List<PackedNativeUnityEngineObject> notSavedObjectList = new List<PackedNativeUnityEngineObject>();
            List<PackedNativeUnityEngineObject> builtinResourcesList = new List<PackedNativeUnityEngineObject>();
            List<PackedNativeUnityEngineObject> unknownObjectList = new List<PackedNativeUnityEngineObject>();
            //List<PackedNativeUnityEngineObject> managerList = new List<PackedNativeUnityEngineObject>();
            for ( int i=0;i< snapshot.nativeObjects.Length;i++)
            {
                PackedNativeUnityEngineObject obj = snapshot.nativeObjects[i];
                if (obj.isPersistent && ((obj.hideFlags & HideFlags.DontUnloadUnusedAsset) == 0))
                {
                    assetsObjectList.Add(obj);
                }
                else if (!obj.isPersistent && obj.hideFlags == HideFlags.None)
                {
                    sceneMemoryObjectList.Add(obj);
                }
                else if (!obj.isPersistent && (obj.hideFlags & HideFlags.HideAndDontSave) != 0)
                {
                    notSavedObjectList.Add(obj);
                }
                else if (obj.isPersistent && (obj.hideFlags & HideFlags.HideAndDontSave) != 0) {
                    builtinResourcesList.Add(obj);
                }
                else
                    unknownObjectList.Add(obj);
            }
            if (unknownObjectList.Count > 0)//I can't find any unknown object yet.
                memoryRootNode.AddNode(unknownRoot);
            for (int i = 0; i < assetsObjectList.Count; i++)
            {
                PackedNativeUnityEngineObject assetsObject = assetsObjectList[i];
                if (!assetGroup.ContainsKey(assetsObject.nativeTypeArrayIndex)) 
					assetGroup.Add(assetsObject.nativeTypeArrayIndex, new Group(assetsObject.nativeTypeArrayIndex,data.mSnapshot.nativeTypes[assetsObject.nativeTypeArrayIndex].name));                
                assetGroup[assetsObject.nativeTypeArrayIndex].packedNativeObjectList.Add(assetsObject);
            }
            for (int i = 0; i < sceneMemoryObjectList.Count; i++)
            {
                PackedNativeUnityEngineObject sceneObject = sceneMemoryObjectList[i];
                if (!sceneMemoryGroup.ContainsKey(sceneObject.nativeTypeArrayIndex))
					sceneMemoryGroup.Add(sceneObject.nativeTypeArrayIndex, new Group(sceneObject.nativeTypeArrayIndex,data.mSnapshot.nativeTypes[sceneObject.nativeTypeArrayIndex].name));
                sceneMemoryGroup[sceneObject.nativeTypeArrayIndex].packedNativeObjectList.Add(sceneObject);
            }
            for (int i = 0; i < notSavedObjectList.Count; i++)
            {
                PackedNativeUnityEngineObject notSavedObject = notSavedObjectList[i];
                if (!notSavedGroup.ContainsKey(notSavedObject.nativeTypeArrayIndex))
                    notSavedGroup.Add(notSavedObject.nativeTypeArrayIndex, new Group(notSavedObject.nativeTypeArrayIndex, data.mSnapshot.nativeTypes[notSavedObject.nativeTypeArrayIndex].name));
                notSavedGroup[notSavedObject.nativeTypeArrayIndex].packedNativeObjectList.Add(notSavedObject);
            }
            for (int i = 0; i < builtinResourcesList.Count; i++)
            {
                PackedNativeUnityEngineObject builtinResourcesObject = builtinResourcesList[i];
                if (!builtinResourcesGroup.ContainsKey(builtinResourcesObject.nativeTypeArrayIndex))
                    builtinResourcesGroup.Add(builtinResourcesObject.nativeTypeArrayIndex, new Group(builtinResourcesObject.nativeTypeArrayIndex, data.mSnapshot.nativeTypes[builtinResourcesObject.nativeTypeArrayIndex].name));
                builtinResourcesGroup[builtinResourcesObject.nativeTypeArrayIndex].packedNativeObjectList.Add(builtinResourcesObject);
            }
            for (int i = 0; i < unknownObjectList.Count; i++)
            {
                PackedNativeUnityEngineObject unknownObject = unknownObjectList[i];
                if (!unknownGroup.ContainsKey(unknownObject.nativeTypeArrayIndex))
                    unknownGroup.Add(unknownObject.nativeTypeArrayIndex, new Group(unknownObject.nativeTypeArrayIndex, data.mSnapshot.nativeTypes[unknownObject.nativeTypeArrayIndex].name));
                unknownGroup[unknownObject.nativeTypeArrayIndex].packedNativeObjectList.Add(unknownObject);
            }
            using (var i = assetGroup.GetEnumerator())//replace foreach
            {
                while (i.MoveNext())
                {
                    Group group = i.Current.Value;
                    SetNodeByClassID(group.classId, group.itemNode, group.packedNativeObjectList);
                    if (group.itemNode != null)
                    {
                        assetsRoot.AddNode(group.itemNode);
                    }
                }
            }
            using (var i = sceneMemoryGroup.GetEnumerator())//replace foreach
            {
                while (i.MoveNext())
                {
                    Group group = i.Current.Value;
                    SetNodeByClassID(group.classId, group.itemNode, group.packedNativeObjectList);
                    if (group.itemNode != null)
                    {
                        sceneMemoryRoot.AddNode(group.itemNode);
                    }
                }
            }
            using (var i = notSavedGroup.GetEnumerator())//replace foreach
            {
                while (i.MoveNext())
                {
                    Group group = i.Current.Value;
                    SetNodeByClassID(group.classId, group.itemNode, group.packedNativeObjectList);
                    if (group.itemNode != null)
                    {
                        notSavedRoot.AddNode(group.itemNode);
                    }
                }
            }
            using (var i = builtinResourcesGroup.GetEnumerator())//replace foreach
            {
                while (i.MoveNext())
                {
                    Group group = i.Current.Value;
                    SetNodeByClassID(group.classId, group.itemNode, group.packedNativeObjectList);
                    if (group.itemNode != null)
                    {
                        builtinResourcesRoot.AddNode(group.itemNode);
                    }
                }
            }
            using (var i = unknownGroup.GetEnumerator())//replace foreach
            {
                while (i.MoveNext())
                {
                    Group group = i.Current.Value;
                    SetNodeByClassID(group.classId, group.itemNode, group.packedNativeObjectList);
                    if (group.itemNode != null)
                    {
                        unknownRoot.AddNode(group.itemNode);
                    }
                }
            }
            memoryRootNode.SetCount();
            memoryRootNode.Convert();
            memoryRootNode.Sort();
            //ClearEditorReferences();//To release gc and memory.
            RemoveNotification();
        }
		void SetNodeByClassID(int classID, PackedItemNode nodeRoot, List<PackedNativeUnityEngineObject> nativeUnityObjectList)
        {
            nodeRoot.Clear();
			nodeRoot.nativeType = data.mSnapshot.nativeTypes [classID];

            int index = -1;
            if (memoryFilters) {
                for (int i = 0; i < memoryFilters.memoryFilterList.Count; i++) {
					if ((int)memoryFilters.memoryFilterList[i].classID == classID)
                    {
                        index = i;
                    }
                }
            }

			if (index > -1)//这样写好蛋疼啊0.0
            {
                Dictionary<PackedItemNode, RegexElement> tempDict = new Dictionary<PackedItemNode, RegexElement>();
                PackedItemNode otherNode = new PackedItemNode("Others");
				otherNode.nativeType = data.mSnapshot.nativeTypes [classID];
                nodeRoot.AddNode(otherNode);
                MemoryFilter memoryFilter = memoryFilters.memoryFilterList[index];
                for (int i = 0; i < memoryFilter.regexElementList.Count; i++)
                {
                    PackedItemNode filterNode = new PackedItemNode(memoryFilter.regexElementList[i].key ,true);
					filterNode.nativeType = data.mSnapshot.nativeTypes [classID];
                    nodeRoot.AddNode(filterNode);
                    tempDict.Add(filterNode, memoryFilter.regexElementList[i]);
                }
                while(nativeUnityObjectList.Count>0)
                {
                    PackedNativeUnityEngineObject item = nativeUnityObjectList[0];
                    string name = item.name;
                    PackedItemNode childNode = new PackedItemNode(name);
                    childNode.size = item.size;
                    childNode.instanceID = item.instanceId;
					childNode.nativeType = data.mSnapshot.nativeTypes [classID];

                    bool isMatch = false;
                    using (var i = tempDict.GetEnumerator())//replace foreach
                    {
                        while (i.MoveNext())
                        {
                            RegexElement regexElement = i.Current.Value;

                            for (int j = 0; j < regexElement.regexList.Count; j++)
                            {
                                if (StringMatchWith(name, regexElement.regexList[j]))
                                {
                                    i.Current.Key.AddNode(childNode);
                                    isMatch = true;
                                    break;
                                }
                            }
                            if (isMatch)
                                break;
                        }
					}
					if (!isMatch) {
						otherNode.AddNode(childNode);
					}
                    nativeUnityObjectList.RemoveAt(0);
                }
            }
            else
            {
                for (int i = 0; i < nativeUnityObjectList.Count; i++)
                {
                    PackedNativeUnityEngineObject item = nativeUnityObjectList[i];
                    string name = item.name;
                    PackedItemNode node = new PackedItemNode(name);
                    node.size = item.size;
                    node.instanceID = item.instanceId;
					node.nativeType = data.mSnapshot.nativeTypes [classID];
                    nodeRoot.AddNode(node);
                }
            }
        }
        static bool StringStartWith(string name,List<string> filter)
        {
            for (int i = 0; i < filter.Count; i++)
            {
                if (string.IsNullOrEmpty(filter[i]))
                    continue;
                if (name.StartsWith(filter[i]))
                    return true;
            }
            return false;
        }
        static bool StringStartWith(string name, string[] filter)
        {
            for (int i = 0; i < filter.Length; i++)
            {
                if (string.IsNullOrEmpty(filter[i]))
                    continue;
                if (name.StartsWith(filter[i]))
                    return true;
            }
            return false;
        }
        static bool StringMatchWith(string name, string filter)
        {
            Regex regex = new Regex(filter, RegexOptions.None);

            for (int i = 0; i < filter.Length; i++)
            {
                if (regex.IsMatch(name))
                    return true;
            }
            return false;
        }
        //MindMap support.
        //Open the site:    naotu.baidu.com    ,and import from the txt.
        void SaveToMindMap(string filePath) {
            File.WriteAllText(filePath, memoryRootNode.ToMindMap(0, canSaveDetails, savedMinSize), System.Text.Encoding.UTF8);
        }
        void LoadFromMindMap(string fileName)
        {
            //data = null;  //reset data
            using (FileStream stream = new FileStream(fileName, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8))
                {
                    Stack<PackedItemNode> stack = new Stack<PackedItemNode>();
                    int lastTabCount = 0;
                    while (reader.Peek() >= 0)
                    {
                        int tabCount = 0;
                        int spaceIndex = 0;
                        string line = reader.ReadLine();
                        char[] charArray = line.ToCharArray();
                        for (int i = 0; i < charArray.Length; i++)
                        {
                            if (charArray[i] == '\t')
                            {
                                tabCount++;
                                continue;
                            }
                            else if (charArray[i] == ' ')
                            {
                                spaceIndex = i;
                            }
                        }
                        string itemName = line.Substring(tabCount, spaceIndex - tabCount);
                        string sizeStr = line.Substring(spaceIndex + 1);
                        PackedItemNode node = new PackedItemNode(itemName);
                        node.sizeStr = sizeStr;
                        if (tabCount == 0 && lastTabCount == 0)
                        {
                            memoryRootNode = node;
                        }
                        else if (tabCount < lastTabCount)
                        {
                            while(lastTabCount-->tabCount)
                                stack.Pop();
                            stack.Pop();
                            stack.Peek().AddNode(node);
                        }
                        else if (tabCount > lastTabCount)
                        {
                            stack.Peek().AddNode(node);
                        }
                        else
                        {
                            stack.Pop();
                            stack.Peek().AddNode(node);
                        }
                        stack.Push(node);
                        lastTabCount = tabCount;
                    }
                    memoryRootNode.SetCount();
                    //memoryRootNode.Convert();
                    //memoryRootNode.Sort();
                }
            }
        }
    }
}