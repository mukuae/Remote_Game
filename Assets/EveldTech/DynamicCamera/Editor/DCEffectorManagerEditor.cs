using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace Eveld.DynamicCamera
{
    [CustomEditor(typeof(DCEffectorManager))]
    public class DCEffectorManagerEditor : Editor
    {

        // Handle colors
        public Color boundaryColor = Color.green;   // color for drawing the effector boundaries
        public Color centerColor = Color.black;
        public Color handleColor1 = Color.red;      // main handles
        public Color handleColor2 = Color.blue;     // off handles
        public Color handleColor3 = Color.yellow;   // off handles
        public Color handleSelectColor = Color.magenta;

        public Color regionColor = Color.red;           // color for the region
        public Color regionHandleColor = Color.black;   // color for the region points

        public Color highLightColor = Color.white;  // highlight color        
        public Color deselectColor = Color.gray;    // deselection color
        
        public const float featherColFactor = 0.707f;   // factor for feather line color, note that this also changes the alpha color if multiplied by a Color

        // Inspector options, starts as folded
        bool foldoutCircle = false;
        bool foldoutSemiCircle = false;
        bool foldoutTorus = false;
        bool foldoutSemiTorus = false;
        bool foldoutBox = false;
        bool foldoutMulti = false;


        DCEffectorManager effectorManager;                          // the main target for this editor script
        SelectionInfo selectionInfo;                                // keeps track of what we have currently selected like effector index, handle, etc..

        public bool visualizeDisplacement = true;                   // visualizes the displacement under the mouse cursor
        public bool visualizeForSelected = true;                    // visualized only for the selected effector
        public bool showRegionInformationInLabel = false;           // shows influence in a label on screen under the mouse
        public bool showBoundingBox = false;                        // show the bounding box

        private Vector3 normal = new Vector3(0, 0, -1);             // direction of the handle normal (for drawing the disks)
        public const float handleConstantScreenSizeFactor = 0.1f;   // determines the radius of the handles
        public float handleRadius;
        public float lineSelectionThickness;
        private const float dottedLineScreenSpaceSize = 2f;
        private const float eps = 1e-5f;

        private const bool byPassIsEnabled = true;                  // this allows us to use methods of the effector even when it is disabled

        // preview cam settings and reference values
        // Known issue: In the universal render pipeline it renders the preview darkish for both settings
        private bool usePreviewCamera = false;
        private bool useSimplePreviewCamera = false;            // simple preview camera always has the skybox as clear flag enabled
        private const string previewCamName = "DCPreviewCam";
        private GameObject effectorCameraGameObject;
        private float effectorCameraPosZ;                       // base offset for the preview camera
        private float effectorCameraOrthoSize;                  // base ortho size for scaling
        private float camPreviewSizeFactor = 1f;                // how large we see the preview

        // material and textures for the camera render to texture
        private Material previewCamMat;
        private Texture2D texture;
        private const int renderTexSize = 512;

        // Mouse and Keyboard Inputs:
        private ButtonState leftMouseButtonState = ButtonState.None;
        private ButtonState rightMouseButtonState = ButtonState.None;

        private ButtonState keyboard_1 = ButtonState.None;  // adds circle effector
        private ButtonState keyboard_2 = ButtonState.None;  // adds semi circle effector
        private ButtonState keyboard_3 = ButtonState.None;  // adds torus effector
        private ButtonState keyboard_4 = ButtonState.None;  // adds semi torus effector
        private ButtonState keyboard_5 = ButtonState.None;  // adds box effector
        private ButtonState keyboard_6 = ButtonState.None;  // adds multi effector

        private ButtonState keyboard_A = ButtonState.None;  // deslects effector
        private ButtonState keyboard_C = ButtonState.None;  // moves effector entirely
        private ButtonState keyboard_B = ButtonState.None;  // deletes an effector
        private ButtonState keyboard_V = ButtonState.None;  // moves the region boundary
        private ButtonState keyboard_D = ButtonState.None;  // deselects this object

        private Operation currentOperation = Operation.None;    // keeps track of the operation we are doing, like dragging, selecting, etc
        
        
        private enum ButtonState
        {
            None, Tap, Hold, Release
        }

        private enum Operation
        {
            None, BlockForFrame, SelectingEffector, MovingEffector, MovingHandle, AddingEffector, InSubGUI,
            AddingMultiEffectorPoint, RemovingMultiEffectorPoint, AddingBoundaryRegionPoint, RemovingBoundaryRegionPoint, MovingBoundaryRegion
        }

        private enum SelectionMode
        {
            None, Sticky, DesectOneLayer
        }


        [MenuItem("Tools/Eveld/Dynamic Camera Effector Tool")]
        public static void AddDynamicCameraToolGameObject()
        {
            Selection.activeGameObject = ObjectFactory.CreateGameObject("DynamicCameraEffectorTool");
            Selection.activeGameObject.AddComponent<DCEffectorManager>();            
        }


        private void OnEnable()
        {
            effectorManager = target as DCEffectorManager;
            selectionInfo = new SelectionInfo();
            Undo.undoRedoPerformed += OnUndoOrRedo;
            
            Tools.hidden = true;    // hides the transform etc gizmos

            if (Camera.main == null)
            {
                Debug.LogWarning("No Main Camera in the scene. The preview camera is unavailable!");
            }
        }

        void OnDisable()
        {
            Tools.hidden = false;

            Undo.undoRedoPerformed -= OnUndoOrRedo;
            
            // Ommitted destroying the preview cam as it raises some exceptions in CameraEditor, this most likely happens because the object is deleted here but it is not yet cleared from the Selection.objects.
            // as we dont use selections anymore to preview, we can delete the camera even though the camera is hidden and not saved anyway
        }

        void OnUndoOrRedo()
        {
            if (selectionInfo.selectedEffectorType != DCEffectorType.None)
            {

                if (selectionInfo.selectedHandleType == DCEffectorHandleType.RegionShaper)
                {
                    DeselectHandle();   // best to deslect the handles upon undo / redo
                }


                switch (selectionInfo.selectedEffectorType)
                {
                    case DCEffectorType.Circle:
                        if (selectionInfo.selectedEffectorIndex >= effectorManager.circleEffectorList.Count)
                        {
                            selectionInfo.selectedEffectorIndex = effectorManager.circleEffectorList.Count - 1;
                        }
                        break;
                    case DCEffectorType.SemiCircle:
                        if (selectionInfo.selectedEffectorIndex >= effectorManager.semiCircleEffectorList.Count)
                        {
                            selectionInfo.selectedEffectorIndex = effectorManager.semiCircleEffectorList.Count - 1;
                        }
                        break;
                    case DCEffectorType.Torus:
                        if (selectionInfo.selectedEffectorIndex >= effectorManager.torusEffectorList.Count)
                        {
                            selectionInfo.selectedEffectorIndex = effectorManager.torusEffectorList.Count - 1;
                        }
                        break;
                    case DCEffectorType.SemiTorus:
                        if (selectionInfo.selectedEffectorIndex >= effectorManager.semiTorusEffectorList.Count)
                        {
                            selectionInfo.selectedEffectorIndex = effectorManager.semiTorusEffectorList.Count - 1;
                        }
                        break;
                    case DCEffectorType.Box:
                        if (selectionInfo.selectedEffectorIndex >= effectorManager.boxEffectorList.Count)
                        {
                            selectionInfo.selectedEffectorIndex = effectorManager.boxEffectorList.Count - 1;
                        }
                        break;
                    case DCEffectorType.Multi:

                       

                        if (selectionInfo.selectedEffectorIndex >= effectorManager.multiEffectorList.Count)
                        {
                            selectionInfo.selectedEffectorIndex = effectorManager.multiEffectorList.Count - 1;                            
                        }

                        if (selectionInfo.selectedEffectorIndex != -1 && selectionInfo.selectedHandleType == DCEffectorHandleType.MultiPointHandle)
                        {
                            DCMultiEffector multiEffector = effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex];
                            if (selectionInfo.selectedMultiEffectorPointIndex >= multiEffector.pathDataList.Count)
                            {
                                selectionInfo.selectedMultiEffectorPointIndex -= 1;
                                if (selectionInfo.selectedMultiEffectorPointIndex == -1) DeselectHandle();
                            }
                        }

                        if (selectionInfo.selectedEffectorIndex > -1)
                        {
                            effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex].UpdateEffector();    // update selected multi effector after undo/redo
                        }


                        break;
                }
                
                if (selectionInfo.selectedEffectorIndex < 0)
                {
                    selectionInfo.selectedEffectorType = DCEffectorType.None;
                }
            }
                        
        }

        
        private void OnSceneGUI()
        {
            Event guiEvent = Event.current;
            
            Vector3 camPos = Camera.current.transform.position;
            Vector3 camPosNoZ = new Vector3(camPos.x, camPos.y, 0);
                        
            handleRadius = HandleUtility.GetHandleSize(camPosNoZ) * handleConstantScreenSizeFactor;            
            lineSelectionThickness = handleRadius / 2;
            
            if (guiEvent.type == EventType.Repaint)
            {                
                Draw();
                OptionsMenuForSelectedEffector();
            }
            else if (guiEvent.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                OptionsMenuForSelectedEffector();
            }
            else
            {
                OptionsMenuForSelectedEffector();   // need to call this three times in this order to prevent some sort of out of order GUI rendering exception
                HandleUtility.Repaint();            // needed for real time updates in the editor window
            }
            
            // Updates all mouse and key states
            leftMouseButtonState = GetMouseButtonState(leftMouseButtonState, 0);
            rightMouseButtonState = GetMouseButtonState(rightMouseButtonState, 1);

            keyboard_1 = GetKeyboardButtonState(keyboard_1, KeyCode.Alpha1);
            keyboard_2 = GetKeyboardButtonState(keyboard_2, KeyCode.Alpha2);
            keyboard_3 = GetKeyboardButtonState(keyboard_3, KeyCode.Alpha3);
            keyboard_4 = GetKeyboardButtonState(keyboard_4, KeyCode.Alpha4);
            keyboard_5 = GetKeyboardButtonState(keyboard_5, KeyCode.Alpha5);
            keyboard_6 = GetKeyboardButtonState(keyboard_6, KeyCode.Alpha6);

            keyboard_A = GetKeyboardButtonState(keyboard_A, KeyCode.A);
            keyboard_C = GetKeyboardButtonState(keyboard_C, KeyCode.C);
            keyboard_B = GetKeyboardButtonState(keyboard_B, KeyCode.B);
            keyboard_V = GetKeyboardButtonState(keyboard_V, KeyCode.V);
            keyboard_D = GetKeyboardButtonState(keyboard_D, KeyCode.D);



            AddEffectorBasedOnKey();    // add effector 
            RemoveSelectedEffector();   // delete effector

            Vector3 mouseWorldPos = GetMouseWorldPositionOnXYPlane();
            
            MouseOverEffectorHandlesCheck(mouseWorldPos);           // check if mouse is over a handle of an effector
            MouseOverSelectedEffectorRegionBoundary(mouseWorldPos); // check if mouse is over handles of the region boundary

            LeftClickOverCurrentMouseOver();        // sets mouse over something to select something in the selection info
            SelectEffector(mouseWorldPos);          // determines if a selected is clicked on left mouse

            DeselectEffector();
            DeselectThisGameObject();

            AddMultiEffectorPathPointOnLeftClick();     // adds a point to the multi effector

            InsertMultiEffectorPathPointOnLeftClick();  // inserts a point to the multi effector
            InsertBoundaryRegionPointOnLeftClick();     // inserts a point to the region boundary

            ClickAndDragMoveSelectedHandle();           // drag a selected handle on left click and drag

            DragCurrentSelectedEffector();              // drags the full effector around
            DragCurrentBoundaryOfSelectedEffector();    // drags only the region of influence boundary

            RemoveMultiEffectorPathPoint();            
            RemoveBoundaryRegionPointOnRightClick();

            // Sets the currentOperation to none after a BlockForFrame operation
            if (currentOperation == Operation.BlockForFrame)
            {
                currentOperation = Operation.None;
            }
        }




        public override void OnInspectorGUI()
        {            
            string helpMessage = "Keyboard keys to add an effector:\nKey 1: Circle Effector\nKey 2: Semi Circle Effector\nKey 3: Torus Effector\nKey 4: Semi Torus Effector\nKey 5: Box Effector\nKey 6: Multi Effector";
            helpMessage += "\n\nControl Keys (when focused in the scene):\nKey A: Deselect the Effector by one stage, this means that the handle is deslected first and on the second press the effector.\nKey D: Deselects the game object from the hierarchy.\nKey B: Delete Current Selected Effector\nKey C: Move the entire effector with the mouse, left click to confirm and press key C again to cancel\nKey V: Move only the regional bounds, left click to confirm and press key V again to cancel";
            helpMessage += "\n\nMouse Controls:\nLeft Mouse: to select effector or handle. Hold left mouse to drag a handle. If a multi effector is selected, use LMB to add points to the effector. If the effector has a bounding region, use LMB to insert points to the region.";
            helpMessage += "\nRight Mouse: For multi effectors and the bounding region, a single right click removes a path node / point if moused over.";
            EditorGUILayout.HelpBox(helpMessage, MessageType.Info);

            GUILayout.BeginHorizontal();
            visualizeDisplacement = GUILayout.Toggle(visualizeDisplacement, "Visualize Displacement", GUILayout.Width(200));
            visualizeForSelected = GUILayout.Toggle(visualizeForSelected, "Visualize Selected Only", GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            showRegionInformationInLabel = GUILayout.Toggle(showRegionInformationInLabel, "Show influence", GUILayout.Width(200));
            showBoundingBox = GUILayout.Toggle(showBoundingBox, "Show Bounding Box", GUILayout.Width(200));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            usePreviewCamera = GUILayout.Toggle(usePreviewCamera, "Use Preview Camera", GUILayout.Width(200));

            // dont use a preview cam if we do not have a main camera in the scene, as the preview camera copies settings from the main camera.
            if (!Camera.main)
            {
                usePreviewCamera = false;
            }

            camPreviewSizeFactor = GUILayout.HorizontalSlider(camPreviewSizeFactor, 0.5f, 3f);
            GUILayout.Label($"{camPreviewSizeFactor:F1} Preview Scale");
            GUILayout.EndHorizontal();

            SetupPreviewCameraSettings();

            int labelwidth = 150;

            foldoutCircle = EditorGUILayout.Foldout(foldoutCircle, $"Show Circle Effectors ({effectorManager.circleEffectorList.Count})");
            if (foldoutCircle)
            {
                InspectorEffectorSelectButtons(effectorManager.circleEffectorList, DCEffectorType.Circle, labelwidth);
            }


            foldoutSemiCircle = EditorGUILayout.Foldout(foldoutSemiCircle, $"Show Semi Circle Effectors ({effectorManager.semiCircleEffectorList.Count})");
            if (foldoutSemiCircle)
            {
                InspectorEffectorSelectButtons(effectorManager.semiCircleEffectorList, DCEffectorType.SemiCircle, labelwidth);
            }

            foldoutTorus = EditorGUILayout.Foldout(foldoutTorus, $"Show Torus Effectors ({effectorManager.torusEffectorList.Count})");
            if (foldoutTorus)
            {
                InspectorEffectorSelectButtons(effectorManager.torusEffectorList, DCEffectorType.Torus, labelwidth);
            }

            foldoutSemiTorus = EditorGUILayout.Foldout(foldoutSemiTorus, $"Show Semi Torus Effectors ({effectorManager.semiTorusEffectorList.Count})");
            if (foldoutSemiTorus)
            {
                InspectorEffectorSelectButtons(effectorManager.semiTorusEffectorList, DCEffectorType.SemiTorus, labelwidth);
            }

            foldoutBox = EditorGUILayout.Foldout(foldoutBox, $"Show Box Effectors ({effectorManager.boxEffectorList.Count})");
            if (foldoutBox)
            {
                InspectorEffectorSelectButtons(effectorManager.boxEffectorList, DCEffectorType.Box, labelwidth);
            }

            foldoutMulti = EditorGUILayout.Foldout(foldoutMulti, $"Show Multi Effectors ({effectorManager.multiEffectorList.Count})");
            if (foldoutMulti)
            {
                InspectorEffectorSelectButtons(effectorManager.multiEffectorList, DCEffectorType.Multi, labelwidth);
            }


            if (GUI.changed)
            {
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Creates buttons in the inspector window for each effector in the list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="effectorList"></param>
        /// <param name="effectorType"></param>
        /// <param name="labelwidth"></param>
        void InspectorEffectorSelectButtons<T>(List<T> effectorList, DCEffectorType effectorType, int labelwidth)
        {
            int deleteIndex = -1;
            for (int i = 0; i < effectorList.Count; i++)
            {

                DCEffector effector = effectorList[i] as DCEffector;

                if (effector != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(effector.name, GUILayout.Width(labelwidth));

                    GUI.enabled = (selectionInfo.selectedEffectorIndex != i || selectionInfo.selectedEffectorType != effectorType) || selectionInfo.selectedEffectorType == DCEffectorType.None;
                    if (GUILayout.Button("Select"))
                    {
                        selectionInfo.selectedEffectorIndex = i;
                        selectionInfo.selectedEffectorType = effectorType;
                        selectionInfo.selectedHandleType = DCEffectorHandleType.None;
                        selectionInfo.selectedMultiEffectorPointIndex = -1;
                    }
                    GUI.enabled = true;

                    bool pressedEnabled = GUILayout.Toggle(effector.isEnabled, "isEnabled");
                    if (pressedEnabled != effector.isEnabled)
                    {
                        Undo.RecordObject(effectorManager, "Effector Enable/Disable");
                        effector.isEnabled = pressedEnabled;
                    }
                    

                    if (GUILayout.Button("Delete"))
                    {
                        deleteIndex = i;
                    }

                    GUILayout.EndHorizontal();
                }
                
            }

            if (deleteIndex != -1)
            {
                Undo.RecordObject(effectorManager, "Delete Effector");
                effectorList.RemoveAt(deleteIndex);
                DeselectAll();
            }
        }


        /// <summary>
        /// Draws the GUI in the main window for adjusting values of the selected effector.
        /// </summary>
        void OptionsMenuForSelectedEffector()
        {
            if (selectionInfo.selectedEffectorType != DCEffectorType.None)
            {

                DCEffector effector = GetSelectedEffector();    // can't return null in this case                     

                DCPropertiesContainer propertiesBefore = new DCPropertiesContainer(effector);
                DCMultiEffectorNodeData nodeDataBefore = null;

                float width = 145;
                float lineHeight = 23;
                float boxWidth = 150;
                float boxHeightMainSettings = 13 * lineHeight;
                
                if (effector is DCCircleEffector)
                {
                    boxHeightMainSettings += lineHeight;                    
                }

                if (effector is DCSemiTorusEffector)
                {
                    boxHeightMainSettings += lineHeight;
                }

                if (effector is DCMultiEffector)
                {
                    boxHeightMainSettings += lineHeight * 2;
                }

                

                float sliderWidth = 100;
                float padding = 5;
                Handles.BeginGUI();
                //Undo.RecordObject(effectorBehaviour, "Sub GUI change"); // object must be recorded before we make changes in sub gui, but now everything gets overwritten
                                
                GUILayout.BeginArea(new Rect(0, 0, boxWidth + padding, boxHeightMainSettings));
                GUILayout.Box("Effector Settings", GUILayout.Width(boxWidth + padding), GUILayout.Height(boxHeightMainSettings));
                GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(5, 25, boxWidth, boxHeightMainSettings));
                GUILayout.Label("Effector Name: ", GUILayout.Width(width));
                effector.name = GUILayout.TextField(effector.name, GUILayout.Width(width), GUILayout.Height(lineHeight));               
                effector.isEnabled = GUILayout.Toggle(effector.isEnabled, "Is Enabled", GUILayout.Width(width), GUILayout.Height(lineHeight));
                GUILayout.Label("Feather Amount: ", GUILayout.Width(boxWidth));
                GUILayout.BeginHorizontal(GUILayout.Width(boxWidth));
                effector.featherAmount = GUILayout.HorizontalSlider(effector.featherAmount, 0, 1, GUILayout.Width(sliderWidth), GUILayout.Height(lineHeight));
                effector.featherAmount = Mathf.Round(effector.featherAmount * 100) / 100;

                GUILayout.Label(effector.featherAmount.ToString("0.00"), GUILayout.Width(40));
                GUILayout.EndHorizontal();
                effector.invertFeatherRegion = GUILayout.Toggle(effector.invertFeatherRegion, "Invert Feather Region", GUILayout.Width(width), GUILayout.Height(lineHeight));
                effector.useRegionAsBounds = GUILayout.Toggle(effector.useRegionAsBounds, "Use Shaped Region", GUILayout.Width(width), GUILayout.Height(lineHeight));

                effector.repel = GUILayout.Toggle(effector.repel, "Repel", GUILayout.Width(width), GUILayout.Height(lineHeight));
                effector.invertStrength = GUILayout.Toggle(effector.invertStrength, "Invert Strength", GUILayout.Width(width), GUILayout.Height(lineHeight));
                effector.distanceFromCenterEqualsStrength = GUILayout.Toggle(effector.distanceFromCenterEqualsStrength, "Dist = Strength", GUILayout.Width(width), GUILayout.Height(lineHeight));
                if (effector.distanceFromCenterEqualsStrength)
                {
                    effector.unilateralDisplacement = false;
                }

                effector.unilateralDisplacement = GUILayout.Toggle(effector.unilateralDisplacement, "Unilateral Displace", GUILayout.Width(width), GUILayout.Height(lineHeight));
                
                if (effector.unilateralDisplacement)
                {
                    effector.distanceFromCenterEqualsStrength = false;
                }

                if (effector is DCCircleEffector)
                {
                    DCCircleEffector circleEffector = (DCCircleEffector)effector;
                    circleEffector.displacementCanCrossCenter = GUILayout.Toggle(circleEffector.displacementCanCrossCenter, "Allow Cross Center", GUILayout.Width(width), GUILayout.Height(lineHeight));                                                         
                }

                if (effector is DCSemiTorusEffector)
                {
                    DCSemiTorusEffector semiCircleEffector = (DCSemiTorusEffector)effector;
                    semiCircleEffector.useStartAndEndCaps = GUILayout.Toggle(semiCircleEffector.useStartAndEndCaps, "Use Circle Caps", GUILayout.Width(width), GUILayout.Height(lineHeight));
                }

                if (effector is DCMultiEffector)
                {
                    DCMultiEffector mutliEffector = (DCMultiEffector)effector;
                    mutliEffector.useStartAndEndCaps = GUILayout.Toggle(mutliEffector.useStartAndEndCaps, "Use Circle Caps", GUILayout.Width(width), GUILayout.Height(lineHeight));
                    mutliEffector.useAsLoop = GUILayout.Toggle(mutliEffector.useAsLoop, "Use As Loop", GUILayout.Width(width), GUILayout.Height(lineHeight));
                }


                GUILayout.EndArea();

                float boxHeightNodeSettings = lineHeight * 4 + padding;
                if (effector is DCTorusEffector)
                {
                    boxHeightNodeSettings = lineHeight * 5 + padding;
                }
                if (effector is DCBoxEffector)
                {
                    boxHeightNodeSettings = lineHeight * 11 + padding;
                }

                if (effector is DCMultiEffector)
                {
                    boxHeightNodeSettings = lineHeight * 9 + padding;
                }

                GUILayout.BeginArea(new Rect(0, boxHeightMainSettings+padding, boxWidth + padding, boxHeightNodeSettings));
                GUILayout.Box("Node Settings", GUILayout.Width(boxWidth + padding), GUILayout.Height(boxHeightNodeSettings));
                GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(5, boxHeightMainSettings+padding, boxWidth, boxHeightNodeSettings));
                EditorGUIUtility.labelWidth = 90;
                EditorGUIUtility.fieldWidth = 60;
                if (effector is DCCircleEffector)
                {
                    DCCircleEffector circleEffector = (DCCircleEffector)effector;
                    circleEffector.strength  = EditorGUI.FloatField(new Rect(0, lineHeight, width, lineHeight), "strength:", circleEffector.strength);                    
                    circleEffector.depthStrength = EditorGUI.FloatField(new Rect(0, lineHeight*2, width, lineHeight), "depth strength:", circleEffector.depthStrength);
                    float radius = EditorGUI.FloatField(new Rect(0, lineHeight * 3, width, lineHeight), "radius:", circleEffector.Radius);                    
                    circleEffector.SetHandleByRadius(Mathf.Max(radius, 0));

                }

                if (effector is DCTorusEffector)
                {
                    DCTorusEffector torusEffector = (DCTorusEffector)effector;
                    torusEffector.strength = EditorGUI.FloatField(new Rect(0, lineHeight, width, lineHeight), "strength:", torusEffector.strength);
                    torusEffector.depthStrength = EditorGUI.FloatField(new Rect(0, lineHeight * 2, width, lineHeight), "depth strength:", torusEffector.depthStrength);
                    float radius = EditorGUI.FloatField(new Rect(0, lineHeight * 3, width, lineHeight), "center radius:", torusEffector.RadiusAtCenterLine);
                    torusEffector.SetHandleByCenterLineRadius(Mathf.Max(radius, 0));
                    float distanceOutward = EditorGUI.FloatField(new Rect(0, lineHeight * 4, width, lineHeight), "distance:", torusEffector.distanceOutward);
                    torusEffector.distanceOutward = Mathf.Clamp(distanceOutward, 0, radius);
                }

                if (effector is DCBoxEffector)
                {
                    DCBoxEffector boxEffector = (DCBoxEffector)effector;
                    EditorGUI.LabelField(new Rect(0, lineHeight, width, lineHeight), "Node 1:");
                    
                    boxEffector.strength1 = EditorGUI.FloatField(new Rect(0, lineHeight * 2, width, lineHeight), "strength:", boxEffector.strength1);
                    boxEffector.depthStrength1 = EditorGUI.FloatField(new Rect(0, lineHeight * 3, width, lineHeight), "depth strength:", boxEffector.depthStrength1);
                    boxEffector.distance1 = EditorGUI.FloatField(new Rect(0, lineHeight * 4, width, lineHeight), "distance out:", boxEffector.distance1);
                    boxEffector.distance1 = Mathf.Max(boxEffector.distance1, 0.01f);
                    if (GUI.Button(new Rect(0, lineHeight*5, width, lineHeight), "Copy to Node 2"))
                    {
                        Undo.RecordObject(effectorManager, "Copy to Node 2");
                        boxEffector.strength2 = boxEffector.strength1;
                        boxEffector.depthStrength2 = boxEffector.depthStrength1;
                        boxEffector.distance2 = boxEffector.distance1;

                    }
                    EditorGUI.LabelField(new Rect(0, lineHeight*6, width, lineHeight), "Node 2:");
                    boxEffector.strength2 = EditorGUI.FloatField(new Rect(0, lineHeight * 7, width, lineHeight), "strength:", boxEffector.strength2);
                    boxEffector.depthStrength2 = EditorGUI.FloatField(new Rect(0, lineHeight * 8, width, lineHeight), "depth strength:", boxEffector.depthStrength2);
                    boxEffector.distance2 = EditorGUI.FloatField(new Rect(0, lineHeight * 9, width, lineHeight), "distance out:", boxEffector.distance2);
                    boxEffector.distance2 = Mathf.Max(boxEffector.distance2, 0.01f);
                    if (GUI.Button(new Rect(0, lineHeight * 10, width, lineHeight), "Copy to Node 1"))
                    {
                        Undo.RecordObject(effectorManager, "Copy to Node 1");
                        boxEffector.strength1 = boxEffector.strength2;
                        boxEffector.depthStrength1 = boxEffector.depthStrength2;
                        boxEffector.distance1 = boxEffector.distance2;
                    }
                }

                if (effector is DCMultiEffector)
                {
                    DCMultiEffector multiEffector = (DCMultiEffector)effector;
                    
                    if (selectionInfo.selectedMultiEffectorPointIndex != -1)
                    {
                        DCMultiEffectorNodeData nodeData = multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex];
                        nodeDataBefore = nodeData.Copy();

                        nodeData.strength = EditorGUI.FloatField(new Rect(0, lineHeight, width, lineHeight), "strength:", nodeData.strength);
                        nodeData.depthStrength = EditorGUI.FloatField(new Rect(0, lineHeight * 2, width, lineHeight), "depth strength:", nodeData.depthStrength);

                        
                        nodeData.desiredDistanceOutwards = EditorGUI.FloatField(new Rect(0, lineHeight * 3, width, lineHeight), "distance out:", nodeData.desiredDistanceOutwards);
                        nodeData.desiredDistanceOutwards = Mathf.Max(nodeData.desiredDistanceOutwards, 0.01f);
                        nodeData.desiredDistancePivot = EditorGUI.FloatField(new Rect(0, lineHeight * 4, width, lineHeight), "distance pivot:", nodeData.desiredDistancePivot);
                        nodeData.desiredDistancePivot = Mathf.Max(nodeData.desiredDistancePivot, 0.01f);
                        EditorGUI.LabelField(new Rect(0, lineHeight*5, width, lineHeight), "Make all nodes:");
                        if (GUI.Button(new Rect(0, lineHeight * 6, width, lineHeight), "same strength"))
                        {
                            Undo.RecordObject(effectorManager, "same strength");
                            for (int i = 0; i < multiEffector.pathDataList.Count; i++)
                            {
                                multiEffector.pathDataList[i].strength = nodeData.strength;
                                multiEffector.pathDataList[i].depthStrength = nodeData.depthStrength;
                            }
                            multiEffector.UpdateEffector(); // need to update the whole effector with the new settings
                        }
                        
                        if (GUI.Button(new Rect(0, lineHeight * 7, width, lineHeight), "same distance outwards"))
                        {
                            Undo.RecordObject(effectorManager, "same distance outwards");
                            for (int i = 0; i < multiEffector.pathDataList.Count; i++)
                            {
                                multiEffector.pathDataList[i].desiredDistanceOutwards = nodeData.desiredDistanceOutwards;
                            }
                            multiEffector.UpdateEffector(); // need to update the whole effector with the new settings
                        }
                        
                        if (GUI.Button(new Rect(0, lineHeight * 8, width, lineHeight), "same pivot distance"))
                        {
                            Undo.RecordObject(effectorManager, "same pivot distance");
                            for (int i = 0; i < multiEffector.pathDataList.Count; i++)
                            {
                                multiEffector.pathDataList[i].desiredDistancePivot = nodeData.desiredDistancePivot;
                            }
                            multiEffector.UpdateEffector(); // need to update the whole effector with the new settings
                        }

                    }
                }

                GUILayout.EndArea();


                if (GUI.changed)
                {
                    // make an undo state upon changing the GUI
                    DCPropertiesContainer propertiesAfter = new DCPropertiesContainer(effector);    // new properties after change
                    // values changed, update effectors
                    if (!propertiesBefore.Equals(propertiesAfter))
                    {
                        propertiesBefore.AssignPropertiesTo(effector);
                        effector.UpdateEffector();
                        Undo.RecordObject(effectorManager, "Sub GUI change");
                        propertiesAfter.AssignPropertiesTo(effector);
                        effector.UpdateEffector();
                    }

                    if (effector is DCMultiEffector)
                    {
                        DCMultiEffector multiEffector = (DCMultiEffector)effector;

                        if (selectionInfo.selectedMultiEffectorPointIndex != -1)
                        {
                            DCMultiEffectorNodeData nodeData = multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex];   // current node data        
                            
                            if (!nodeData.Equals(nodeDataBefore))
                            {
                                multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex] = nodeDataBefore;                     // ndoe data before change
                                multiEffector.UpdateEffector();
                                Undo.RecordObject(effectorManager, "Node GUI change");
                                multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex] = nodeData;
                                multiEffector.UpdateEffector();
                            }
                           
                        }
                    }

                }

                Handles.EndGUI();
                
            }

        }


        Vector3 GetMouseWorldPositionOnXYPlane(float drawPlaneDepth = 0)
        {
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            float dstToDrawPlane = (drawPlaneDepth - mouseRay.origin.z) / mouseRay.direction.z;
            return mouseRay.GetPoint(dstToDrawPlane);
        }

        Vector3 GetMouseWorldPositionOnXYPlane(Vector2 screenOffset, float drawPlaneDepth = 0)
        {
            Ray mouseRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition + screenOffset);
            float dstToDrawPlane = (drawPlaneDepth - mouseRay.origin.z) / mouseRay.direction.z;
            return mouseRay.GetPoint(dstToDrawPlane);
        }

        /// <summary>
        /// Gets the selected effector, if not selected anything returns null
        /// </summary>
        /// <returns>The selected effector or null</returns>
        DCEffector GetSelectedEffector()
        {
            switch (selectionInfo.selectedEffectorType)
            {
                case DCEffectorType.Circle:
                    return effectorManager.circleEffectorList[selectionInfo.selectedEffectorIndex];
                case DCEffectorType.SemiCircle:
                    return effectorManager.semiCircleEffectorList[selectionInfo.selectedEffectorIndex];
                case DCEffectorType.Torus:
                    return effectorManager.torusEffectorList[selectionInfo.selectedEffectorIndex];                    
                case DCEffectorType.SemiTorus:
                    return effectorManager.semiTorusEffectorList[selectionInfo.selectedEffectorIndex];                    
                case DCEffectorType.Box:
                    return effectorManager.boxEffectorList[selectionInfo.selectedEffectorIndex];
                case DCEffectorType.Multi:
                    return effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex];
                default:
                    return null;
            }
        }


        /// <summary>
        /// Adds an effector if the correct key is pressed and conditios are met.
        /// </summary>
        void AddEffectorBasedOnKey()
        {
            Event guiEvent = Event.current;
            if (IsMouseInsideCurrentScreen() && guiEvent.modifiers == EventModifiers.None && guiEvent.type == EventType.KeyDown && leftMouseButtonState == ButtonState.None && currentOperation == Operation.None)
            {                
                Vector3 mouseWorldPosition = GetMouseWorldPositionOnXYPlane();

                if (keyboard_1 == ButtonState.Tap) 
                {
                    Undo.RecordObject(effectorManager, "New Circle Effector");
                    DCCircleEffector circleEffector = new DCCircleEffector();
                    circleEffector.name = "Circle Effector";
                    circleEffector.MoveEffectorTo(mouseWorldPosition);
                    circleEffector.UpdateEffector();
                    effectorManager.circleEffectorList.Add(circleEffector);
                    DeselectAll();
                    selectionInfo.selectedEffectorType = DCEffectorType.Circle;
                    selectionInfo.selectedHandleType = DCEffectorHandleType.None;
                    selectionInfo.selectedEffectorIndex = effectorManager.circleEffectorList.Count - 1;
                    currentOperation = Operation.AddingEffector;
                }

                else if (keyboard_2 == ButtonState.Tap)
                {
                    Undo.RecordObject(effectorManager, "New Semi Circle Effector");
                    DCSemiCircleEffector semiCircleEffector = new DCSemiCircleEffector();
                    semiCircleEffector.name = "Semi Circle Effector";
                    semiCircleEffector.MoveEffectorTo(mouseWorldPosition);
                    semiCircleEffector.UpdateEffector();
                    effectorManager.semiCircleEffectorList.Add(semiCircleEffector);
                    DeselectAll();
                    selectionInfo.selectedEffectorType = DCEffectorType.SemiCircle;
                    selectionInfo.selectedHandleType = DCEffectorHandleType.None;
                    selectionInfo.selectedEffectorIndex = effectorManager.semiCircleEffectorList.Count-1;
                    currentOperation = Operation.AddingEffector;
                }

                else if (keyboard_3 == ButtonState.Tap)
                {
                    Undo.RecordObject(effectorManager, "New Torus Effector");
                    DCTorusEffector torusEffector = new DCTorusEffector();
                    torusEffector.name = "Torus Effector";
                    torusEffector.MoveEffectorTo(mouseWorldPosition);
                    torusEffector.UpdateEffector();
                    effectorManager.torusEffectorList.Add(torusEffector);
                    DeselectAll();
                    selectionInfo.selectedEffectorType = DCEffectorType.Torus;
                    selectionInfo.selectedHandleType = DCEffectorHandleType.None;
                    selectionInfo.selectedEffectorIndex = effectorManager.torusEffectorList.Count - 1;
                    currentOperation = Operation.AddingEffector;
                }

                else if (keyboard_4 == ButtonState.Tap)
                {
                    Undo.RecordObject(effectorManager, "New Semi Torus Effector");
                    DCSemiTorusEffector semiTorusEffector = new DCSemiTorusEffector();
                    semiTorusEffector.name = "Semi Torus Effector";
                    semiTorusEffector.MoveEffectorTo(mouseWorldPosition);
                    semiTorusEffector.UpdateEffector();
                    effectorManager.semiTorusEffectorList.Add(semiTorusEffector);
                    DeselectAll();
                    selectionInfo.selectedEffectorType = DCEffectorType.SemiTorus;
                    selectionInfo.selectedHandleType = DCEffectorHandleType.None;
                    selectionInfo.selectedEffectorIndex = effectorManager.semiTorusEffectorList.Count - 1;
                    currentOperation = Operation.AddingEffector;
                }

                else if (keyboard_5 == ButtonState.Tap)
                {
                    Undo.RecordObject(effectorManager, "New Box Effector");
                    DCBoxEffector boxEffector = new DCBoxEffector();
                    boxEffector.name = "Box Effector";
                    boxEffector.MoveEffectorTo(mouseWorldPosition);
                    boxEffector.UpdateEffector();
                    effectorManager.boxEffectorList.Add(boxEffector);
                    DeselectAll();
                    selectionInfo.selectedEffectorType = DCEffectorType.Box;
                    selectionInfo.selectedHandleType = DCEffectorHandleType.None;
                    selectionInfo.selectedEffectorIndex = effectorManager.boxEffectorList.Count - 1;
                    currentOperation = Operation.AddingEffector;
                }

                else if (keyboard_6 == ButtonState.Tap)
                {
                    Undo.RecordObject(effectorManager, "New Multi Effector");
                    DCMultiEffector multiEffector = new DCMultiEffector();                    
                    multiEffector.name = "Multi Effector";
                    multiEffector.InitializeTwoNodes(true);
                    multiEffector.MoveEffectorTo(mouseWorldPosition);
                    multiEffector.UpdateEffector();
                    effectorManager.multiEffectorList.Add(multiEffector);
                    DeselectAll();
                    selectionInfo.selectedEffectorType = DCEffectorType.Multi;
                    selectionInfo.selectedHandleType = DCEffectorHandleType.None;
                    selectionInfo.selectedEffectorIndex = effectorManager.multiEffectorList.Count - 1;
                    currentOperation = Operation.AddingEffector;
                }                
            }
            else
            {
                if (currentOperation == Operation.AddingEffector)
                {
                    currentOperation = Operation.None;
                }
            }

            if (guiEvent.type == EventType.KeyDown && guiEvent.keyCode == KeyCode.Alpha2)
            {
                Event.current.Use();    // disables alpha2 key in unity window 
            }
        }

        void RemoveSelectedEffector()
        {
            if (currentOperation == Operation.None)
            {
                if (selectionInfo.selectedEffectorType != DCEffectorType.None && keyboard_B == ButtonState.Tap)
                {
                    if (selectionInfo.selectedEffectorType == DCEffectorType.Circle)
                    {
                        Undo.RecordObject(effectorManager, "Delete Circle Effector");
                        effectorManager.circleEffectorList.RemoveAt(selectionInfo.selectedEffectorIndex);
                        DeselectAll();
                    }

                    if (selectionInfo.selectedEffectorType == DCEffectorType.SemiCircle)
                    {
                        Undo.RecordObject(effectorManager, "Delete Semi Circle Effector");
                        effectorManager.semiCircleEffectorList.RemoveAt(selectionInfo.selectedEffectorIndex);
                        selectionInfo.selectedEffectorType = DCEffectorType.None;
                        DeselectAll();
                    }

                    if (selectionInfo.selectedEffectorType == DCEffectorType.Torus)
                    {
                        Undo.RecordObject(effectorManager, "Delete Torus Effector");
                        effectorManager.torusEffectorList.RemoveAt(selectionInfo.selectedEffectorIndex);
                        DeselectAll();
                    }

                    if (selectionInfo.selectedEffectorType == DCEffectorType.SemiTorus)
                    {
                        Undo.RecordObject(effectorManager, "Delete Semi Torus Effector");
                        effectorManager.semiTorusEffectorList.RemoveAt(selectionInfo.selectedEffectorIndex);
                        DeselectAll();
                    }

                    if (selectionInfo.selectedEffectorType == DCEffectorType.Box)
                    {
                        Undo.RecordObject(effectorManager, "Delete Box Effector");
                        effectorManager.boxEffectorList.RemoveAt(selectionInfo.selectedEffectorIndex);
                        DeselectAll();
                    }

                    if (selectionInfo.selectedEffectorType == DCEffectorType.Multi)
                    {
                        Undo.RecordObject(effectorManager, "Delete Multi Effector");
                        effectorManager.multiEffectorList.RemoveAt(selectionInfo.selectedEffectorIndex);
                        DeselectAll();
                    }
                }
            }
        }

        void DeselectThisGameObject()
        {
            if (keyboard_D == ButtonState.Tap && leftMouseButtonState == ButtonState.None && rightMouseButtonState == ButtonState.None && currentOperation == Operation.None)
            {
                Selection.objects = new Object[0];
            }
        }

        void DeselectEffector()
        {
            if (keyboard_A == ButtonState.Tap && leftMouseButtonState == ButtonState.None && rightMouseButtonState == ButtonState.None && currentOperation == Operation.None)
            {                
                if (selectionInfo.selectedHandleType != DCEffectorHandleType.None)
                {
                    DeselectHandle();
                }
                else
                {
                    DeselectAll();
                }
                
            }

        }

        void DeselectHandle()
        {
            selectionInfo.selectedHandleType = DCEffectorHandleType.None;            
            selectionInfo.selectedMultiEffectorPointIndex = -1;
            selectionInfo.ResetRegionSelectionInfo();
        }

        void DeselectHandle(SelectionMode selectionMode)
        {
            if (selectionMode == SelectionMode.None)
            {
                DeselectHandle();
            }
            else if (selectionMode == SelectionMode.DesectOneLayer)
            {
                if (selectionInfo.selectedHandleType == DCEffectorHandleType.MultiRadiusPivotHandle || selectionInfo.selectedHandleType == DCEffectorHandleType.MultiOutwardDistanceHandle)
                {
                    selectionInfo.selectedHandleType = DCEffectorHandleType.MultiPointHandle;
                }
            }
        }

        void DeselectAll()
        {
            DeselectHandle();
            selectionInfo.selectedEffectorIndex = -1;
            selectionInfo.selectedEffectorType = DCEffectorType.None;
            selectionInfo.selectedMultiEffectorPointIndex = -1;
            selectionInfo.ResetMultiPointSelectionInfo();
            selectionInfo.ResetRegionSelectionInfo();
        }


        void SelectEffector(Vector2 position)
        {
            
            // selects the first effector if the point is inside of it
            if (leftMouseButtonState == ButtonState.Tap && selectionInfo.selectedEffectorType == DCEffectorType.None)
            {
                // circle
                for (int i = 0; i < effectorManager.circleEffectorList.Count; i++)
                {
                    if (effectorManager.circleEffectorList[i].IsInsideEffector(position, byPassIsEnabled))
                    {
                        currentOperation = Operation.SelectingEffector;
                        selectionInfo.selectedEffectorType = DCEffectorType.Circle;
                        selectionInfo.selectedEffectorIndex = i;
                        return;
                    }
                }

                // semi circle
                for (int i = 0; i < effectorManager.semiCircleEffectorList.Count; i++)
                {
                    if (effectorManager.semiCircleEffectorList[i].IsInsideEffector(position, byPassIsEnabled))
                    {
                        currentOperation = Operation.SelectingEffector;
                        selectionInfo.selectedEffectorType = DCEffectorType.SemiCircle;
                        selectionInfo.selectedEffectorIndex = i;
                        return;
                    }
                }

                // Torus
                for (int i = 0; i < effectorManager.torusEffectorList.Count; i++)
                {
                    if (effectorManager.torusEffectorList[i].IsInsideEffector(position, byPassIsEnabled))
                    {
                        currentOperation = Operation.SelectingEffector;
                        selectionInfo.selectedEffectorType = DCEffectorType.Torus;
                        selectionInfo.selectedEffectorIndex = i;
                        return;
                    }
                }


                // Semi Torus
                for (int i = 0; i < effectorManager.semiTorusEffectorList.Count; i++)
                {
                    if (effectorManager.semiTorusEffectorList[i].IsInsideEffector(position, byPassIsEnabled))
                    {
                        currentOperation = Operation.SelectingEffector;
                        selectionInfo.selectedEffectorType = DCEffectorType.SemiTorus;
                        selectionInfo.selectedEffectorIndex = i;
                        return;
                    }
                }

                // Box
                for (int i = 0; i < effectorManager.boxEffectorList.Count; i++)
                {
                    if (effectorManager.boxEffectorList[i].IsInsideEffector(position, byPassIsEnabled))
                    {
                        currentOperation = Operation.SelectingEffector;
                        selectionInfo.selectedEffectorType = DCEffectorType.Box;
                        selectionInfo.selectedEffectorIndex = i;
                        return;
                    }
                }

                // Multi
                for (int i = 0; i < effectorManager.multiEffectorList.Count; i++)
                {                    
                    if (effectorManager.multiEffectorList[i].IsInsideBoundingBox(position, byPassIsEnabled) && effectorManager.multiEffectorList[i].IsInsideEffector(position, byPassIsEnabled))
                    {
                        currentOperation = Operation.SelectingEffector;
                        selectionInfo.selectedEffectorType = DCEffectorType.Multi;
                        selectionInfo.selectedEffectorIndex = i;
                        return;
                    }
                }

            }
            else if (currentOperation == Operation.SelectingEffector)
            {
                currentOperation = Operation.None;
            }

        }
        

        /// <summary>
        /// Confirms the mouse over information to the selected information of the press of a button
        /// </summary>
        void LeftClickOverCurrentMouseOver()
        {            
            if (selectionInfo.mouseOverHandleType != DCEffectorHandleType.None && leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None)
            {
                if (selectionInfo.mouseOverHandleType == DCEffectorHandleType.MultiSegmentHandle)
                {
                    return; // dont select anyting when hovering over the secondary handles as we want to keep the multiEffectorPointIndex selected
                }
                currentOperation = Operation.SelectingEffector;
                selectionInfo.selectedEffectorType = selectionInfo.mouseOverEffectorType;
                selectionInfo.selectedEffectorIndex = selectionInfo.mouseOverEffectorIndex;
                selectionInfo.selectedHandleType = selectionInfo.mouseOverHandleType;
                selectionInfo.selectedMultiEffectorPointIndex = selectionInfo.mouseOverMultiEffectorPointIndex;

                selectionInfo.selectedRegionPointIndex = selectionInfo.mouseOverRegionPointIndex;

            }
            else if (currentOperation == Operation.SelectingEffector)
            {
                currentOperation = Operation.None;
            }
        }

        void MouseOverEffectorHandlesCheck(Vector2 position)
        {            
            float handleRadiusSqr = handleRadius * handleRadius;

            // set default values for mouse over check
            selectionInfo.ResetMouseOverInfo();

            if (leftMouseButtonState != ButtonState.Hold && currentOperation == Operation.None)
            {
                bool handleInside = false;

                // circle list
                for (int i = 0; i < effectorManager.circleEffectorList.Count; i++)
                {
                    DCCircleEffector circleEffector = effectorManager.circleEffectorList[i];

                    if ((position - circleEffector.positionCenter).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType= DCEffectorHandleType.CircleHandleCenter;
                    }

                    if ((position - circleEffector.positionRadiusHandle1).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.CircleHandleRadius1;
                    }

                    if (handleInside)
                    {
                        selectionInfo.mouseOverEffectorType = DCEffectorType.Circle;
                        selectionInfo.mouseOverEffectorIndex = i;
                        return;
                    }
                }

                // semi circle list
                for (int i = 0; i < effectorManager.semiCircleEffectorList.Count; i++)
                {
                    DCSemiCircleEffector semiCircleEffector = effectorManager.semiCircleEffectorList[i];

                    if ((position - semiCircleEffector.positionCenter).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.CircleHandleCenter;
                    }

                    if ((position - semiCircleEffector.positionRadiusHandle1).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.CircleHandleRadius1;
                    }

                    if ((position - semiCircleEffector.positionRadiusHandle2).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.CircleHandleRadius2;
                    }

                    if (handleInside)
                    {
                        selectionInfo.mouseOverEffectorType = DCEffectorType.SemiCircle;
                        selectionInfo.mouseOverEffectorIndex = i;
                        return;
                    }
                }

                // torus list
                for (int i = 0; i < effectorManager.torusEffectorList.Count; i++)
                {
                    DCTorusEffector torusEffector = effectorManager.torusEffectorList[i];


                    float outerRadius = torusEffector.distanceOutward + torusEffector.RadiusAtCenterLine;
                    float pointRadius = (position - torusEffector.positionCenterOfRotation).magnitude;

                    if (pointRadius > outerRadius - lineSelectionThickness && pointRadius < outerRadius + lineSelectionThickness)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.TorusHandleDistance;
                    }

                    if ((position - torusEffector.positionCenterOfRotation).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.TorusHandleCenter;
                    }

                    if ((position - torusEffector.positionCenterRadiusHandle1).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.TorusHandleRadius1;
                    }

                    if (handleInside)
                    {
                        selectionInfo.mouseOverEffectorType = DCEffectorType.Torus;
                        selectionInfo.mouseOverEffectorIndex = i;
                        return;
                    }
                }


                // semi torus list
                for (int i = 0; i < effectorManager.semiTorusEffectorList.Count; i++)
                {
                    DCSemiTorusEffector semiTorusEffector = effectorManager.semiTorusEffectorList[i];

                    float outerRadius = semiTorusEffector.distanceOutward + semiTorusEffector.RadiusAtCenterLine;
                    float pointRadius = (position - semiTorusEffector.positionCenterOfRotation).magnitude;

                    if (semiTorusEffector.IsPointInPizzaSlice(position) && pointRadius > outerRadius - lineSelectionThickness && pointRadius < outerRadius + lineSelectionThickness)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.TorusHandleDistance;
                    }


                    if ((position - semiTorusEffector.positionCenterOfRotation).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.TorusHandleCenter;
                    }

                    if ((position - semiTorusEffector.positionCenterRadiusHandle1).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.TorusHandleRadius1;
                    }

                    if ((position - semiTorusEffector.positionCenterRadiusHandle2).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.TorusHandleRadius2;
                    }

                    if (handleInside)
                    {
                        selectionInfo.mouseOverEffectorType = DCEffectorType.SemiTorus;
                        selectionInfo.mouseOverEffectorIndex = i;
                        return;
                    }
                }

                // box effector
                for (int i = 0; i < effectorManager.boxEffectorList.Count; i++)
                {
                    DCBoxEffector boxEffector = effectorManager.boxEffectorList[i];

                    if ((position - boxEffector.position1).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.BoxHandlePosition1;
                    }

                    if ((position - boxEffector.position2).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.BoxHandlePosition2;
                    }

                    if ((position - boxEffector.positionDistanceHandle1A).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.BoxHandleDistance1A;
                    }

                    if ((position - boxEffector.positionDistanceHandle1B).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.BoxHandleDistance1B;
                    }

                    if ((position - boxEffector.positionDistanceHandle2A).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.BoxHandleDistance2A;
                    }

                    if ((position - boxEffector.positionDistanceHandle2B).sqrMagnitude < handleRadiusSqr)
                    {
                        handleInside = true;
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.BoxHandleDistance2B;
                    }

                    if (handleInside)
                    {
                        selectionInfo.mouseOverEffectorType = DCEffectorType.Box;
                        selectionInfo.mouseOverEffectorIndex = i;
                        return;
                    }
                }

                // multi effector
                for (int i = 0; i < effectorManager.multiEffectorList.Count; i++)
                {
                    DCMultiEffector multiEffector = effectorManager.multiEffectorList[i];
                    List<DCMultiEffectorNodeData> pathDataList = multiEffector.pathDataList;
                    for (int j = 0; j < pathDataList.Count; j++)
                    {
                        DCMultiEffectorNodeData nodeData = pathDataList[j];
                        Vector2 p1 = nodeData.point;
                        
                        // can only mouse over connecting lines if we already selected the effector
                        if (selectionInfo.selectedEffectorType == DCEffectorType.Multi && i == selectionInfo.selectedEffectorIndex)
                        {
                            Vector2 p2 = pathDataList[(j+1) % pathDataList.Count].point;
                            bool calcDistFromSegment = true;
                            if (j == pathDataList.Count - 1 && !multiEffector.useAsLoop)
                            {
                                calcDistFromSegment = false;
                            }
                            Vector2 dP = (p2 - p1).normalized;
                            Vector2 p1offset = p1 + dP * handleRadius * 2.1f;
                            Vector2 p2offset = p2 - dP * handleRadius * 2.1f;

                            /*
                            if(j < pathDataList.Count - 1 || multiEffector.useAsLoop)
                            {
                                DCBoxEffector boxEffector = multiEffector.boxEffectorList[j];
                                p1offset = boxEffector.position1;
                                if ((p1offset - p1).sqrMagnitude < handleRadiusSqr)
                                {
                                    p1offset += dP * handleRadius * 2.1f;
                                }
                                p2offset = boxEffector.position2;
                                if ((p2offset - p2).sqrMagnitude < handleRadiusSqr)
                                {
                                    p2offset -= dP * handleRadius * 2.1f;
                                }
                            }*/

                            // HandlesUtility.DistancePointToLineSegment(position, p1offset, p2offset)
                            

                            if (calcDistFromSegment && DistanceSquareOnSegmentProjected(p1offset, p2offset, position) < handleRadiusSqr)
                            {
                                handleInside = true;
                                selectionInfo.mouseOverHandleType = DCEffectorHandleType.MultiSegmentHandle;
                                selectionInfo.multiEffectorLineIndex = j;
                                selectionInfo.mouseIsOverMultiEffectorLine = true;
                            }
                        }


                        // main point of the multi effector path
                        if ((position - p1).sqrMagnitude < handleRadiusSqr)
                        {
                            handleInside = true;
                            selectionInfo.mouseOverHandleType = DCEffectorHandleType.MultiPointHandle;
                            selectionInfo.mouseOverMultiEffectorPointIndex = j;
                        }

                        // pivot handle of the main point when the main point is selected already
                        if (GetMultiEffectorPivotHandle(j, nodeData, out Vector2 pivotHandle))
                        {
                            if ((position - pivotHandle).sqrMagnitude < handleRadiusSqr)
                            {
                                handleInside = true;
                                selectionInfo.mouseOverHandleType = DCEffectorHandleType.MultiRadiusPivotHandle;
                                selectionInfo.mouseOverMultiEffectorPointIndex = j;
                            }
                        }

                        // distance handle of the main point when the main point is selected already
                        if (GetMultiEffectorOutwardDistanceHandle(j, pathDataList, out Vector2 distanceHandle))
                        {
                            if ((position - distanceHandle).sqrMagnitude < handleRadiusSqr)
                            {
                                handleInside = true;
                                selectionInfo.mouseOverHandleType = DCEffectorHandleType.MultiOutwardDistanceHandle;
                                selectionInfo.mouseOverMultiEffectorPointIndex = j;
                            }
                        }



                        if (handleInside)
                        {
                            selectionInfo.mouseOverEffectorType = DCEffectorType.Multi;
                            selectionInfo.mouseOverEffectorIndex = i;                            
                            return;
                        }
                    }
                }
            }
        }


        private bool MouseOverSelectedEffectorRegionBoundary(Vector2 position)
        {
            DCEffector effector = GetSelectedEffector();
            if (effector == null) return false;

            if (effector.useRegionAsBounds)
            {
                float handleRadiusSqr = handleRadius * handleRadius;
                List<Vector2> pointsList = effector.effectorBoundaryRegion.points;
                for (int i = 0; i < pointsList.Count; i++)
                {
                    Vector2 p1 = pointsList[i];
                    Vector2 p2 = pointsList[(i + 1) % pointsList.Count];

                    if ((position - p1).sqrMagnitude <= handleRadiusSqr)
                    {
                        selectionInfo.mouseOverEffectorType = selectionInfo.selectedEffectorType;   // must keep it selected
                        selectionInfo.mouseOverEffectorIndex = selectionInfo.selectedEffectorIndex; // must keep it selected
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.RegionShaper;
                        selectionInfo.mouseOverRegionPointIndex = i;
                        return true;
                    }


                    // create some margins on the line to select so it does not interfere with the points
                    Vector2 dP = (p2 - p1).normalized;
                    Vector2 p1offset = p1 + dP * handleRadius * 2.1f;
                    Vector2 p2offset = p2 - dP * handleRadius * 2.1f;

                    if (DistanceSquareOnSegmentProjected(p1offset, p2offset, position) <= handleRadiusSqr)
                    {
                        selectionInfo.mouseOverEffectorType = selectionInfo.selectedEffectorType;   // must keep it selected
                        selectionInfo.mouseOverEffectorIndex = selectionInfo.selectedEffectorIndex; // must keep it selected
                        selectionInfo.mouseOverHandleType = DCEffectorHandleType.RegionShaper;                        
                        selectionInfo.mouseOverRegionLineIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }


        void ClickAndDragMoveSelectedHandle()
        {
            
            if (leftMouseButtonState != ButtonState.None)
            {


                DragRegionBoundsHandle();

                // Circle
                if (selectionInfo.selectedEffectorType == DCEffectorType.Circle)
                {

                    DCCircleEffector circleEffector = effectorManager.circleEffectorList[selectionInfo.selectedEffectorIndex];

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.CircleHandleCenter)
                    {
                        DragPointHandle(circleEffector, ref circleEffector.positionCenter, "Circle Center Move");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.CircleHandleRadius1)
                    {
                        DragPointHandle(circleEffector, ref circleEffector.positionRadiusHandle1, "Circle Radius Handle Move");
                    }

                }

                // Semi Circle
                if (selectionInfo.selectedEffectorType == DCEffectorType.SemiCircle)
                {
                    DCSemiCircleEffector semiCircleEffector = effectorManager.semiCircleEffectorList[selectionInfo.selectedEffectorIndex];

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.CircleHandleCenter)
                    {
                        DragPointHandle(semiCircleEffector, ref semiCircleEffector.positionCenter, "Semi Circle Center Move");                        
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.CircleHandleRadius1)
                    {
                        DragPointHandle(semiCircleEffector, ref semiCircleEffector.positionRadiusHandle1, "Semi Circle Radius Handle Move");                       
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.CircleHandleRadius2)
                    {
                        DragPointHandle(semiCircleEffector, ref semiCircleEffector.positionRadiusHandle2, "Semi Circle Angle Handle Move");
                    }
                }

                // TORUS
                if (selectionInfo.selectedEffectorType == DCEffectorType.Torus)
                {
                    DCTorusEffector torusEffector = effectorManager.torusEffectorList[selectionInfo.selectedEffectorIndex];

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleCenter)
                    {
                        DragPointHandle(torusEffector, ref torusEffector.positionCenterOfRotation, "Torus Center Move");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleRadius1)
                    {
                        DragPointHandle(torusEffector, ref torusEffector.positionCenterRadiusHandle1, "Torus Radius Move");
                    }


                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleDistance)
                    {
                        
                        if (leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None)
                        {
                            Undo.RecordObject(effectorManager, "Torus Distance Outward");
                            selectionInfo.positionAtStartOfDrag = GetMouseWorldPositionOnXYPlane();
                            selectionInfo.valueAtStartOfDrag = torusEffector.distanceOutward;
                            currentOperation = Operation.MovingHandle;
                        }

                        if (currentOperation == Operation.MovingHandle)
                        {
                            Vector2 dirCenterToStartDrag = ((Vector2)selectionInfo.positionAtStartOfDrag - torusEffector.positionCenterOfRotation).normalized;
                            Vector2 dirMouseToStartDrag = (GetMouseWorldPositionOnXYPlane() - selectionInfo.positionAtStartOfDrag);

                            torusEffector.distanceOutward = Mathf.Clamp(Vector2.Dot(dirCenterToStartDrag, dirMouseToStartDrag) + selectionInfo.valueAtStartOfDrag, 0, torusEffector.RadiusAtCenterLine);
                            torusEffector.UpdateEffector();

                            if (leftMouseButtonState == ButtonState.Release)
                            {
                                currentOperation = Operation.None;
                                DeselectHandle();
                            }

                        }
                        
                    }
                }

                // Semi Torus
                if (selectionInfo.selectedEffectorType == DCEffectorType.SemiTorus)
                {
                    
                    DCSemiTorusEffector semiTorusEffector = effectorManager.semiTorusEffectorList[selectionInfo.selectedEffectorIndex];

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleCenter)
                    {
                        DragPointHandle(semiTorusEffector, ref semiTorusEffector.positionCenterOfRotation, "Torus Center Move");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleRadius1)
                    {
                        DragPointHandle(semiTorusEffector, ref semiTorusEffector.positionCenterRadiusHandle1, "Torus Radius Move");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleRadius2)
                    {
                        DragPointHandle(semiTorusEffector, ref semiTorusEffector.positionCenterRadiusHandle2, "Torus Angle Move");
                    }


                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleDistance)
                    {
                        SlidePointHandle(semiTorusEffector, ref semiTorusEffector.distanceOutward, GetMouseWorldPositionOnXYPlane(), semiTorusEffector.positionCenterOfRotation, "Semi Torus Distance Outward", 0, semiTorusEffector.RadiusAtCenterLine);
                    }
                }
                // Box
                if (selectionInfo.selectedEffectorType == DCEffectorType.Box)
                {
                    DCBoxEffector boxEffector = effectorManager.boxEffectorList[selectionInfo.selectedEffectorIndex];
                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.BoxHandlePosition1)
                    {
                        DragPointHandle(boxEffector, ref boxEffector.position1, "Box Node1 Move");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.BoxHandlePosition2)
                    {
                        DragPointHandle(boxEffector, ref boxEffector.position2, "Box Node2 Move");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.BoxHandleDistance1A)
                    {
                        SlidePointHandle(boxEffector, ref boxEffector.distance1, boxEffector.positionDistanceHandle1A, boxEffector.position1, "Node1 Distance Outward");                       
                    }
                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.BoxHandleDistance1B)
                    {
                        SlidePointHandle(boxEffector, ref boxEffector.distance1, boxEffector.positionDistanceHandle1B, boxEffector.position1, "Node1 Distance Outward");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.BoxHandleDistance2A)
                    {
                        SlidePointHandle(boxEffector, ref boxEffector.distance2, boxEffector.positionDistanceHandle2A, boxEffector.position2, "Node2 Distance Outward");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.BoxHandleDistance2B)
                    {
                        SlidePointHandle(boxEffector, ref boxEffector.distance2, boxEffector.positionDistanceHandle2B, boxEffector.position2, "Node2 Distance Outward");
                    }
                }

                // Multi effector
                if (selectionInfo.selectedEffectorType == DCEffectorType.Multi)
                {
                    DCMultiEffector multiEffector = effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex];

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.MultiPointHandle)
                    {
                        DragPointHandle(multiEffector, ref multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex].point, "Node Drag");
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.MultiRadiusPivotHandle)
                    {
                        GetMultiEffectorPivotHandle(selectionInfo.selectedMultiEffectorPointIndex, multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex], out Vector2 pivotHandle);                        
                        SlidePointHandle(multiEffector, ref multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex].desiredDistancePivot, pivotHandle, multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex].point, "Node Pivot Distance", 0.1f, selectionMode: SelectionMode.DesectOneLayer);                        
                    }

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.MultiOutwardDistanceHandle)
                    {
                        GetMultiEffectorOutwardDistanceHandle(selectionInfo.selectedMultiEffectorPointIndex, multiEffector.pathDataList, out Vector2 distanceHandle);
                        SlidePointHandle(multiEffector, ref multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex].desiredDistanceOutwards, distanceHandle, multiEffector.pathDataList[selectionInfo.selectedMultiEffectorPointIndex].point, "Node Distance Outward", 0.1f, selectionMode: SelectionMode.DesectOneLayer);
                    }

                }
            }
        }


        void DragRegionBoundsHandle()
        {
            if (selectionInfo.selectedEffectorType != DCEffectorType.None && selectionInfo.selectedHandleType == DCEffectorHandleType.RegionShaper)
            {
                if (selectionInfo.selectedRegionPointIndex > -1)
                {
                    DCEffector effector = GetSelectedEffector();
                    List<Vector2> points = effector.effectorBoundaryRegion.points;
                    Vector2 P = points[selectionInfo.selectedRegionPointIndex];
                    //DragPointHandle(effector, ref P, "Boundary Drag");

                    // the following code is DragPointHandle(effector, ref P, "Boundary Drag"); but slightly adjusted in the Undo state so it function properly
                    if (leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None && ((Vector2)GetMouseWorldPositionOnXYPlane() - P).sqrMagnitude < handleRadius * handleRadius)
                    {
                        selectionInfo.positionAtStartOfDrag = (Vector2)GetMouseWorldPositionOnXYPlane() - P;
                        selectionInfo.positionHandleAtStartOfDrag = P;
                        currentOperation = Operation.MovingHandle;
                    }


                    if (currentOperation == Operation.MovingHandle)
                    {
                        if (leftMouseButtonState == ButtonState.Release)
                        {
                            points[selectionInfo.selectedRegionPointIndex] = selectionInfo.positionHandleAtStartOfDrag;
                            effector.UpdateEffector();
                            Undo.RecordObject(effectorManager, "Boundary Drag");
                            currentOperation = Operation.None;
                        }

                        P = GetMouseWorldPositionOnXYPlane() - selectionInfo.positionAtStartOfDrag;
                        points[selectionInfo.selectedRegionPointIndex] = P;
                        effector.UpdateEffector();
                    }
                    // END of the same code as DragPointHandle()
                }
            }
        }



        void DragCurrentSelectedEffector()
        {
            DCEffector effector = GetSelectedEffector();

            if (effector != null)
            {
                if (keyboard_C == ButtonState.Tap && currentOperation == Operation.None)
                {
                    // use drag info to store the information of the initial drag
                    selectionInfo.positionAtStartOfDrag = effector.rootPosition;
                    selectionInfo.positionHandleAtStartOfDrag = effector.rootPosition - (Vector2)GetMouseWorldPositionOnXYPlane();
                    currentOperation = Operation.MovingEffector;    
                }

                else if (currentOperation == Operation.MovingEffector)
                {
                    if (leftMouseButtonState == ButtonState.Tap)
                    {
                        effector.MoveEffectorTo(selectionInfo.positionAtStartOfDrag);   // place the effector back to log an undo state
                        Undo.RecordObject(effectorManager, "Move Effector");
                        currentOperation = Operation.BlockForFrame;
                    }

                    effector.MoveEffectorTo(GetMouseWorldPositionOnXYPlane() + selectionInfo.positionHandleAtStartOfDrag);

                    // tap the same move key to cancel
                    if (keyboard_C == ButtonState.Tap)
                    {
                        effector.MoveEffectorTo(selectionInfo.positionAtStartOfDrag);
                        currentOperation = Operation.BlockForFrame;
                    }
                }                
            }            
        }

        void DragCurrentBoundaryOfSelectedEffector()
        {
            DCEffector effector = GetSelectedEffector();

            if (effector != null && effector.useRegionAsBounds)
            {

                if (keyboard_V == ButtonState.Tap && currentOperation == Operation.None)
                {   
                    // use drag info to store the information of the initial drag
                    selectionInfo.positionAtStartOfDrag = effector.effectorBoundaryRegion.rootPosition;
                    selectionInfo.positionHandleAtStartOfDrag = effector.effectorBoundaryRegion.rootPosition - (Vector2)GetMouseWorldPositionOnXYPlane();
                    currentOperation = Operation.MovingBoundaryRegion;

                }

                else if (currentOperation == Operation.MovingBoundaryRegion)
                {
                    if (leftMouseButtonState == ButtonState.Tap)
                    {
                        effector.effectorBoundaryRegion.MoveBoundaryTo(selectionInfo.positionAtStartOfDrag); // place the boundary back to its initial state at drag for undo log
                        Undo.RecordObject(effectorManager, "Move Boundary Region");
                        currentOperation = Operation.BlockForFrame;
                    }

                    effector.effectorBoundaryRegion.MoveBoundaryTo(GetMouseWorldPositionOnXYPlane() + selectionInfo.positionHandleAtStartOfDrag);

                    // tap the same move key to cancel
                    if (keyboard_V == ButtonState.Tap)
                    {
                        effector.effectorBoundaryRegion.MoveBoundaryTo(selectionInfo.positionAtStartOfDrag);    
                        currentOperation = Operation.BlockForFrame;
                    }

                }
            }
        }


        private void DragPointHandle(DCEffector effector, ref Vector2 handlePosition, string undoMessage)
        {            
            if (leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None && ((Vector2)GetMouseWorldPositionOnXYPlane() - handlePosition).sqrMagnitude < handleRadius * handleRadius)
            {
                selectionInfo.positionAtStartOfDrag = (Vector2)GetMouseWorldPositionOnXYPlane() - handlePosition;
                selectionInfo.positionHandleAtStartOfDrag = handlePosition;
                currentOperation = Operation.MovingHandle;
            }


            if (currentOperation == Operation.MovingHandle)
            {
                if (leftMouseButtonState == ButtonState.Release)
                {
#pragma warning disable IDE0059 // Unnecessary assignment of a value
                    handlePosition = selectionInfo.positionHandleAtStartOfDrag; // might seem unnecesarry but it is not. It is required to make the Undo state
#pragma warning restore IDE0059 // Unnecessary assignment of a value
                    effector.UpdateEffector();
                    Undo.RecordObject(effectorManager, undoMessage);
                    currentOperation = Operation.None;
                    //DeselectHandle();
                }

                handlePosition = GetMouseWorldPositionOnXYPlane() - selectionInfo.positionAtStartOfDrag;
                effector.UpdateEffector();
            }            
        }

        /// <summary>
        /// Slide a handle on the XY plane
        /// </summary>
        /// <param name="effector">Current effector</param>
        /// <param name="slideValue">Value that will be set based on the slide amount</param>
        /// <param name="initialReferencePositionOfDrag">The start position of the drag</param>
        /// <param name="slideDirectionRefPoint">The position that makes up the slide direction</param>
        /// <param name="undoMessage"></param>
        /// <param name="minValue"></param>
        /// <param name="maxValue"></param>
        private void SlidePointHandle(DCEffector effector, ref float slideValue, Vector3 initialReferencePositionOfDrag, Vector3 slideDirectionRefPoint, string undoMessage, float minValue = 0, float maxValue = float.MaxValue, SelectionMode selectionMode = SelectionMode.None)
        {
            if (leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None)
            {
                //Undo.RecordObject(effectorBehaviour, undoMessage);
                selectionInfo.positionAtStartOfDrag = initialReferencePositionOfDrag;// GetMouseWorldPositionOnXYPlane(); // mouse world position makes the slide direction behave according to the click position and not hte handle position center. This can give unexpected behaviour in slide direction
                selectionInfo.valueAtStartOfDrag = slideValue;
                currentOperation = Operation.MovingHandle;
            }

            if (currentOperation == Operation.MovingHandle)
            {

                if (leftMouseButtonState == ButtonState.Release)
                {
#pragma warning disable IDE0059 // Unnecessary assignment of a value
                    slideValue = selectionInfo.valueAtStartOfDrag; // might seem unnecesarry but it is not. It is required to make the Undo state
#pragma warning restore IDE0059 // Unnecessary assignment of a value
                    effector.UpdateEffector();
                    Undo.RecordObject(effectorManager, undoMessage);
                    currentOperation = Operation.None;
                    DeselectHandle(selectionMode);
                }

                Vector2 dirCenterToStartDrag = ((Vector2)selectionInfo.positionAtStartOfDrag - (Vector2)slideDirectionRefPoint).normalized;
                if (dirCenterToStartDrag.sqrMagnitude == 0)
                {
                    dirCenterToStartDrag = Vector2.right;
                }
                Vector2 dirMouseToStartDrag = (GetMouseWorldPositionOnXYPlane() - selectionInfo.positionAtStartOfDrag);
                
                slideValue = Mathf.Clamp(Vector2.Dot(dirCenterToStartDrag, dirMouseToStartDrag) + selectionInfo.valueAtStartOfDrag, minValue, maxValue);
                effector.UpdateEffector();                
            }
        }



        void AddMultiEffectorPathPointOnLeftClick()
        {
            if (selectionInfo.selectedEffectorType == DCEffectorType.Multi)
            {
                DCMultiEffector multiEffector = effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex];

                if (leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None && selectionInfo.mouseOverHandleType == DCEffectorHandleType.None && selectionInfo.selectedHandleType == DCEffectorHandleType.None && !selectionInfo.mouseIsOverMultiEffectorLine)
                {
                    currentOperation = Operation.AddingMultiEffectorPoint;
                    Undo.RecordObject(effectorManager, "Add Path Node");
                    int previousIndex = multiEffector.pathDataList.Count-1;
                    multiEffector.AddPathPoint(GetMouseWorldPositionOnXYPlane());
                    
                    if (previousIndex >= 0)
                    {
                        DCMultiEffectorNodeData nodeData = multiEffector.pathDataList[previousIndex];
                        multiEffector.SetDesiredOutwardDistanceAt(previousIndex + 1, nodeData.desiredDistanceOutwards);
                        multiEffector.SetDesiredPivotDistanceAt(previousIndex + 1, nodeData.desiredDistancePivot);
                    }

                    multiEffector.UpdateEffector(); // update the multi effector to reflect changes
                }
                else if (currentOperation == Operation.AddingMultiEffectorPoint)
                {
                    currentOperation = Operation.None;
                }
            }            
        }

        void InsertMultiEffectorPathPointOnLeftClick()
        {            
            if (selectionInfo.selectedEffectorType == DCEffectorType.Multi)
            {
                DCMultiEffector multiEffector = effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex];

                if (leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None && selectionInfo.mouseOverHandleType == DCEffectorHandleType.MultiSegmentHandle && selectionInfo.mouseIsOverMultiEffectorLine && selectionInfo.selectedHandleType == DCEffectorHandleType.None)
                {
                    currentOperation = Operation.AddingMultiEffectorPoint;
                    Undo.RecordObject(effectorManager, "Insert Path Node");
                    int previousIndex = selectionInfo.multiEffectorLineIndex;
                    multiEffector.InsertPathNodeAt(selectionInfo.multiEffectorLineIndex + 1, GetMouseWorldPositionOnXYPlane());

                    if (previousIndex >= 0)
                    {
                        DCMultiEffectorNodeData nodeData = multiEffector.pathDataList[previousIndex];
                        multiEffector.SetDesiredOutwardDistanceAt(previousIndex + 1, nodeData.desiredDistanceOutwards);
                        multiEffector.SetDesiredPivotDistanceAt(previousIndex + 1, nodeData.desiredDistancePivot);
                    }

                    multiEffector.UpdateEffector(); // update the multi effector to reflect changes
                }
                else if (currentOperation == Operation.AddingMultiEffectorPoint)
                {
                    currentOperation = Operation.None;
                }
            }
        }


        void RemoveMultiEffectorPathPoint()
        {
            if (selectionInfo.selectedEffectorType == DCEffectorType.Multi)
            {
                DCMultiEffector multiEffector = effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex];

                if (rightMouseButtonState == ButtonState.Tap && currentOperation == Operation.None && selectionInfo.mouseOverHandleType == DCEffectorHandleType.MultiPointHandle && selectionInfo.mouseOverEffectorType == selectionInfo.selectedEffectorType)
                {
                    if (multiEffector.pathDataList.Count > 2)
                    {
                        currentOperation = Operation.RemovingMultiEffectorPoint;
                        Undo.RecordObject(effectorManager, "Remove Path Node");
                        multiEffector.RemovePathNodeAt(selectionInfo.mouseOverMultiEffectorPointIndex);
                        multiEffector.UpdateEffector(); // update the multi effector to reflect changes
                        DeselectHandle();
                    }                                    
                }
                else if (currentOperation == Operation.RemovingMultiEffectorPoint)
                {
                    currentOperation = Operation.None;
                }
            }
        }




        void InsertBoundaryRegionPointOnLeftClick()
        {
            if (selectionInfo.selectedEffectorType != DCEffectorType.None)
            {
                DCEffector effector = GetSelectedEffector();

                if (effector == null)
                {
                    return;
                }

                if (leftMouseButtonState == ButtonState.Tap && currentOperation == Operation.None && selectionInfo.mouseOverHandleType == DCEffectorHandleType.RegionShaper && selectionInfo.mouseOverRegionLineIndex != -1)
                {
                    //currentOperation = Operation.AddingBoundaryRegionPoint;   // disabled this so we can drag right after inserting
                    Undo.RecordObject(effectorManager, "Insert Boundary Point");
                    effector.effectorBoundaryRegion.InsertPointAt(selectionInfo.mouseOverRegionLineIndex + 1, GetMouseWorldPositionOnXYPlane());
                    selectionInfo.selectedRegionPointIndex = selectionInfo.mouseOverRegionLineIndex + 1;
                }
                else if (currentOperation == Operation.AddingBoundaryRegionPoint)
                {
                    currentOperation = Operation.None;
                }
            }
        }

        void RemoveBoundaryRegionPointOnRightClick()
        {
            if (selectionInfo.selectedEffectorType != DCEffectorType.None)
            {

                DCEffector effector = GetSelectedEffector();

                if (effector == null)
                {
                    return;
                }

                if (rightMouseButtonState == ButtonState.Tap && currentOperation == Operation.None && selectionInfo.mouseOverHandleType == DCEffectorHandleType.RegionShaper && selectionInfo.mouseOverRegionPointIndex != -1)
                {
                    
                    currentOperation = Operation.RemovingBoundaryRegionPoint;
                    Undo.RecordObject(effectorManager, "Remove Boundary Point");
                    effector.effectorBoundaryRegion.RemovePointAt(selectionInfo.mouseOverRegionPointIndex);
                    DeselectHandle();
                    
                }
                else if (currentOperation == Operation.RemovingBoundaryRegionPoint)
                {
                    currentOperation = Operation.None;
                }
            }
        }


        /// <summary>
        /// Checks if the mouse is in the current window
        /// </summary>
        /// <returns></returns>
        bool IsMouseInsideCurrentScreen()
        {
            Vector2 mousePos = Event.current.mousePosition;
            return (mousePos.x >= 0 && mousePos.x <= Screen.width && mousePos.y >= 0 && mousePos.y <= Screen.height);
        }


        ButtonState GetMouseButtonState(ButtonState mouseButtonState, int mouseButton)
        {
            Event guiEvent = Event.current;
            if (guiEvent.button == mouseButton && guiEvent.type == EventType.MouseDown && mouseButtonState == ButtonState.None)
            {
                mouseButtonState = ButtonState.Tap;
            }
            else if ( mouseButtonState == ButtonState.Tap)
            {
                mouseButtonState = ButtonState.Hold;
            }

            if (guiEvent.button == mouseButton && guiEvent.type == EventType.MouseUp && mouseButtonState == ButtonState.Hold)
            {
                mouseButtonState = ButtonState.Release;
            }
            else if (mouseButtonState == ButtonState.Release)
            {
                mouseButtonState = ButtonState.None;
            }

            return mouseButtonState;
        }


        ButtonState GetKeyboardButtonState(ButtonState keyboardButtonState, KeyCode keyCode)
        {
            Event guiEvent = Event.current;
            if (guiEvent.keyCode == keyCode && guiEvent.type == EventType.KeyDown && keyboardButtonState == ButtonState.None)
            {                
                keyboardButtonState = ButtonState.Tap;
            }
            else if (keyboardButtonState == ButtonState.Tap)
            {
                keyboardButtonState = ButtonState.Hold;
            }

            if (guiEvent.keyCode == keyCode && guiEvent.type == EventType.KeyUp && keyboardButtonState == ButtonState.Hold)
            {
                keyboardButtonState = ButtonState.Release;
            }
            else if (keyboardButtonState == ButtonState.Release)
            {
                keyboardButtonState = ButtonState.None;
            }

            return keyboardButtonState;
        }


        /// <summary>
        /// Draw all stuff that will be seen in the editor
        /// </summary>
        void Draw()
        {
            DrawCircleEffectors();
            DrawSemiCircleEffectors();
            DrawTorusEffectors();
            DrawSemiTorusEffectors();
            DrawBoxEffectors();
            DrawMultiEffectors();
            DrawDisplacement(visualizeForSelected);
            DrawRegionBoundingBoxIfSelected();
            if (showBoundingBox)
            {
                DrawBoundingBoxes();
            }

            if (useSimplePreviewCamera)
            {
                DrawPreviewCameraSimple();  // this works 100%
            }
            else
            {
                DrawPreviewCamera();        // this works although it could clip in 2D view
            }
            
            
        }


       
        void DrawDisplacementOfSelectedEffector()
        {
            DCEffector effector = GetSelectedEffector();

            if (effector == null) return;

            Vector3 mouseWorldPos = GetMouseWorldPositionOnXYPlane();

            effector.GetDisplacementAt(mouseWorldPos, out DCEffectorOutputData effectorOutputData);
            DrawDisplacementInfo(effectorOutputData);
        }

        /// <summary>
        /// Draws the displacement
        /// </summary>
        /// <param name="selectedOnly"></param>
        void DrawDisplacement(bool selectedOnly)
        {
            Vector3 mouseWorldPos = GetMouseWorldPositionOnXYPlane();

            if (selectedOnly)
            {
                DrawDisplacementOfSelectedEffector();
            }
            else
            {
                DCEffectorOutputData effectorOutputData;
                effectorOutputData.displacement = Vector3.zero;
                effectorOutputData.lockedXY = false;
                effectorOutputData.influence = 0;

                
                List<DCEffector> insideEffectorList = new List<DCEffector>();
                // circle
                for (int i = 0; i < effectorManager.circleEffectorList.Count; i++)
                {
                    if (effectorManager.circleEffectorList[i].IsInsideEffector(mouseWorldPos))
                    {
                        insideEffectorList.Add(effectorManager.circleEffectorList[i]);
                    }
                }

                // semi circle
                for (int i = 0; i < effectorManager.semiCircleEffectorList.Count; i++)
                {
                    if (effectorManager.semiCircleEffectorList[i].IsInsideEffector(mouseWorldPos))
                    {
                        insideEffectorList.Add(effectorManager.semiCircleEffectorList[i]);
                    }
                }

                // torus
                for (int i = 0; i < effectorManager.torusEffectorList.Count; i++)
                {
                    if (effectorManager.torusEffectorList[i].IsInsideEffector(mouseWorldPos))
                    {
                        insideEffectorList.Add(effectorManager.torusEffectorList[i]);
                    }
                }
                // semi torus
                for (int i = 0; i < effectorManager.semiTorusEffectorList.Count; i++)
                {
                    if (effectorManager.semiTorusEffectorList[i].IsInsideEffector(mouseWorldPos))
                    {
                        insideEffectorList.Add(effectorManager.semiTorusEffectorList[i]);
                    }
                }
                // box
                for (int i = 0; i < effectorManager.boxEffectorList.Count; i++)
                {
                    if (effectorManager.boxEffectorList[i].IsInsideEffector(mouseWorldPos))
                    {
                        insideEffectorList.Add(effectorManager.boxEffectorList[i]);
                    }
                }
                // multi
                for (int i = 0; i < effectorManager.multiEffectorList.Count; i++)
                {
                    if (effectorManager.multiEffectorList[i].IsInsideEffector(mouseWorldPos))
                    {
                        insideEffectorList.Add(effectorManager.multiEffectorList[i]);
                    }
                }

                DCEffector.GetWeightedAverageDisplacement(mouseWorldPos, insideEffectorList, true, out effectorOutputData);

                DrawDisplacementInfo(effectorOutputData);                
            }
        }


        /// <summary>
        /// Draws the influence and depth in a label
        /// </summary>
        /// <param name="effectorOutputData"></param>
        public void DrawDisplacementInfo(DCEffectorOutputData effectorOutputData)
        {
            Vector3 mouseWorldPos = GetMouseWorldPositionOnXYPlane();
            if (effectorOutputData.displacement.sqrMagnitude != 0)
            {
                // split components into displacement in Z and displacement in XY
                Vector3 displacementXY = effectorOutputData.displacement;
                float depthZ = displacementXY.z;
                Vector3 displacementZ = new Vector3(0, 0, displacementXY.z);
                displacementXY.z = 0;

                if (visualizeDisplacement)
                {
                    Handles.color = Color.yellow;
                    DCEditorHandleDrawFunctions.DrawArrowXY(mouseWorldPos, mouseWorldPos + displacementXY, 1);
                    Handles.color = Color.white;
                    DCEditorHandleDrawFunctions.DrawArrowXZ(mouseWorldPos + displacementXY, mouseWorldPos + displacementXY + displacementZ, 1);

                }

                if (usePreviewCamera && effectorCameraGameObject != null)
                {
                    if (effectorCameraGameObject.GetComponent<Camera>().orthographic)
                    {
                        effectorCameraGameObject.transform.position = new Vector3(mouseWorldPos.x, mouseWorldPos.y, effectorCameraPosZ) + displacementXY;
                        effectorCameraGameObject.GetComponent<Camera>().orthographicSize = Mathf.Max(effectorCameraOrthoSize - depthZ * DCEffector.depthToOrthgrapicSizeFactor, 0.1f);
                    }
                    else
                    {
                        effectorCameraGameObject.transform.position = new Vector3(mouseWorldPos.x, mouseWorldPos.y, effectorCameraPosZ) + displacementXY + displacementZ;
                    }
                }

                // show information in labels under the mouse cursor
                if (showRegionInformationInLabel)
                {
                    Handles.Label(GetMouseWorldPositionOnXYPlane(new Vector2(10.0f, 15.0f)), $"Influence: {effectorOutputData.influence:F3}");
                    Handles.Label(GetMouseWorldPositionOnXYPlane(new Vector2(10.0f, 30.0f)), $"Z depth: {displacementZ.z:F3}");
                }

            }
            else
            {
                // move the camera around
                if (usePreviewCamera && effectorCameraGameObject != null)
                {
                    effectorCameraGameObject.transform.position = new Vector3(mouseWorldPos.x, mouseWorldPos.y, effectorCameraPosZ);
                }
            }
        }


        void DrawRegionBoundingBoxIfSelected()
        {
            DCEffector effector;

            switch (selectionInfo.selectedEffectorType)
            {
                case DCEffectorType.Circle:
                    effector = effectorManager.circleEffectorList[selectionInfo.selectedEffectorIndex];
                    break;
                case DCEffectorType.SemiCircle:
                    effector = effectorManager.semiCircleEffectorList[selectionInfo.selectedEffectorIndex];
                    break;
                case DCEffectorType.Torus:
                    effector = effectorManager.torusEffectorList[selectionInfo.selectedEffectorIndex];
                    break;
                case DCEffectorType.SemiTorus:
                    effector = effectorManager.semiTorusEffectorList[selectionInfo.selectedEffectorIndex];
                    break;
                case DCEffectorType.Box:
                    effector = effectorManager.boxEffectorList[selectionInfo.selectedEffectorIndex];
                    break;
                case DCEffectorType.Multi:
                    effector = effectorManager.multiEffectorList[selectionInfo.selectedEffectorIndex];
                    break;
                default:
                    return;
            }

            if (effector.useRegionAsBounds)
            {
                List<Vector2> pointsList = effector.effectorBoundaryRegion.points;
                for (int i = 0; i < pointsList.Count; i++)
                {
                    Vector2 p = pointsList[i];
                    Handles.color = (selectionInfo.mouseOverRegionLineIndex == i && currentOperation == Operation.None ) ? highLightColor : regionColor;
                    Handles.DrawDottedLine(pointsList[i], pointsList[(i + 1) % pointsList.Count], dottedLineScreenSpaceSize);
                    

                    Rect rect = new Rect(p.x - handleRadius / 2, p.y - handleRadius / 2, handleRadius, handleRadius);
                    Color fillColor = regionColor;//regionHandleColor;
                    if (selectionInfo.selectedRegionPointIndex == i)
                    {
                        fillColor = handleSelectColor;
                    }
                    else if (selectionInfo.mouseOverRegionPointIndex == i && currentOperation == Operation.None)
                    {
                        fillColor = highLightColor;
                    }
                    Handles.color = fillColor;
                    //Handles.DrawSolidRectangleWithOutline(rect, fillColor, regionColor);
                    Vector3[] verts = new Vector3[] {
                            new Vector3(p.x - handleRadius / 2, p.y - handleRadius / 2, 0),
                            new Vector3(p.x - handleRadius / 2, p.y + handleRadius / 2, 0),
                            new Vector3(p.x + handleRadius / 2, p.y + handleRadius / 2, 0),
                            new Vector3(p.x + handleRadius / 2, p.y - handleRadius / 2, 0)
                        };

                    Handles.DrawSolidRectangleWithOutline(verts, fillColor, regionHandleColor);
                }
            }
        }


        void DrawCircleEffectors()
        {
            List<DCCircleEffector> circleEffectorList = effectorManager.circleEffectorList;
            for (int i = 0; i < circleEffectorList.Count; i++)
            {
                DCCircleEffector circleEffector = circleEffectorList[i];

                if (selectionInfo.selectedEffectorType == DCEffectorType.Circle && i == selectionInfo.selectedEffectorIndex)
                {
                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.CircleHandleCenter, handleColor1);
                    Handles.DrawSolidDisc(circleEffector.positionCenter, normal, handleRadius);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.CircleHandleRadius1, handleColor1);
                    Handles.DrawSolidDisc(circleEffector.positionRadiusHandle1, normal, handleRadius);

                    Handles.color = handleColor1;
                    Handles.DrawDottedLine(circleEffector.positionCenter, circleEffector.positionRadiusHandle1, dottedLineScreenSpaceSize);

                    Handles.color = boundaryColor;
                    Handles.DrawWireArc(circleEffector.positionCenter, normal, circleEffector.positionRadiusHandle1, 360, circleEffector.Radius);
                    Handles.color = boundaryColor * featherColFactor;
                    Handles.DrawWireArc(circleEffector.positionCenter, normal, circleEffector.positionRadiusHandle1, 360, circleEffector.Radius * circleEffector.featherAmount);

                    Handles.color = Color.white;
                    Vector3 depthPoint = new Vector3(circleEffector.positionCenter.x, circleEffector.positionCenter.y, circleEffector.depthStrength);                    
                    DCEditorHandleDrawFunctions.DrawArrowXZ(circleEffector.positionCenter, depthPoint, 1);

                }
                else
                {
                    bool isEffectorMousedOver = i == selectionInfo.mouseOverEffectorIndex && selectionInfo.mouseOverEffectorType == DCEffectorType.Circle;
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.CircleHandleCenter, isEffectorMousedOver);
                    Handles.DrawSolidDisc(circleEffector.positionCenter, normal, handleRadius);
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.CircleHandleRadius1, isEffectorMousedOver);
                    Handles.DrawSolidDisc(circleEffector.positionRadiusHandle1, normal, handleRadius);
                    Handles.DrawDottedLine(circleEffector.positionCenter, circleEffector.positionRadiusHandle1, dottedLineScreenSpaceSize);

                    Handles.color = deselectColor;
                    Handles.DrawWireArc(circleEffector.positionCenter, normal, circleEffector.positionRadiusHandle1, 360, circleEffector.Radius);
                    Handles.color = deselectColor * featherColFactor;
                    Handles.DrawWireArc(circleEffector.positionCenter, normal, circleEffector.positionRadiusHandle1, 360, circleEffector.Radius * circleEffector.featherAmount);
                }
                

            }
        }


        void DrawSemiCircleEffectors()
        {
            List<DCSemiCircleEffector> semiCircleEffectorList = effectorManager.semiCircleEffectorList;
            for (int i = 0; i < semiCircleEffectorList.Count; i++)
            {
                DCSemiCircleEffector semiCircleEffector = semiCircleEffectorList[i];


                if (selectionInfo.selectedEffectorType == DCEffectorType.SemiCircle && i == selectionInfo.selectedEffectorIndex)
                {
                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.CircleHandleCenter, handleColor1);
                    Handles.DrawSolidDisc(semiCircleEffector.positionCenter, normal, handleRadius);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.CircleHandleRadius1, handleColor1);
                    Handles.DrawSolidDisc(semiCircleEffector.positionRadiusHandle1, normal, handleRadius);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.CircleHandleRadius2, handleColor2);
                    Handles.DrawSolidDisc(semiCircleEffector.positionRadiusHandle2, normal, handleRadius);

                    Handles.color = handleColor1;
                    Handles.DrawDottedLine(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle1, dottedLineScreenSpaceSize);

                    Handles.color = handleColor2;
                    Handles.DrawDottedLine(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle2, dottedLineScreenSpaceSize);

                    Handles.color = boundaryColor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle1,
                                                                    semiCircleEffector.positionRadiusHandle2, semiCircleEffector.Radius, true);

                    Handles.color = boundaryColor * featherColFactor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle1,
                                                                    semiCircleEffector.positionRadiusHandle2, semiCircleEffector.Radius * semiCircleEffector.featherAmount, true);

                    Handles.color = Color.white;
                    Vector3 depthPoint = new Vector3(semiCircleEffector.positionCenter.x, semiCircleEffector.positionCenter.y, semiCircleEffector.depthStrength);
                    DCEditorHandleDrawFunctions.DrawArrowXZ(semiCircleEffector.positionCenter, depthPoint, 1);
                }
                else
                {
                    // deselection colors
                    bool isEfffectorMousedOver = i == selectionInfo.mouseOverEffectorIndex && selectionInfo.mouseOverEffectorType == DCEffectorType.SemiCircle;
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.CircleHandleCenter, isEfffectorMousedOver);             
                    Handles.DrawSolidDisc(semiCircleEffector.positionCenter, normal, handleRadius);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.CircleHandleRadius1, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(semiCircleEffector.positionRadiusHandle1, normal, handleRadius);
                    Handles.DrawDottedLine(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle1, dottedLineScreenSpaceSize);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.CircleHandleRadius2, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(semiCircleEffector.positionRadiusHandle2, normal, handleRadius);
                    Handles.DrawDottedLine(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle2, dottedLineScreenSpaceSize);

                    Handles.color = deselectColor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle1,
                                                                    semiCircleEffector.positionRadiusHandle2, semiCircleEffector.Radius, true);

                    Handles.color = deselectColor * featherColFactor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector.positionCenter, semiCircleEffector.positionRadiusHandle1,
                                                                    semiCircleEffector.positionRadiusHandle2, semiCircleEffector.Radius * semiCircleEffector.featherAmount, true);

                }




                
            }
        }


        void DrawTorusEffectors()
        {
            List<DCTorusEffector> torusEffectorList = effectorManager.torusEffectorList;
            for (int i = 0; i < torusEffectorList.Count; i++)
            {
                
                DCTorusEffector torusEffector = torusEffectorList[i];



                if (selectionInfo.selectedEffectorType == DCEffectorType.Torus && i == selectionInfo.selectedEffectorIndex)
                {
                    //Handles.color = (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleCenter) ? handleSelectColor : (selectionInfo.mouseOverHandleType == DCEffectorHandleType.TorusHandleCenter) ? highLightColor : handleColor1;
                    //if (selectionInfo.mouseOverHandleType == DCEffectorHandleType.TorusHandleCenter && selectionInfo.selectedHandleType != DCEffectorHandleType.TorusHandleCenter)  Handles.color = highLightColor;
                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.TorusHandleCenter, handleColor1);

                    if (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleCenter)
                    {
                        Handles.color = handleSelectColor;
                    }
                    else if (selectionInfo.mouseOverEffectorIndex == selectionInfo.selectedEffectorIndex && selectionInfo.mouseOverHandleType == DCEffectorHandleType.TorusHandleCenter)
                    {
                        Handles.color = highLightColor;
                    }
                    else
                    {
                        Handles.color = handleColor1;
                    }

                    Handles.DrawSolidDisc(torusEffector.positionCenterOfRotation, normal, handleRadius);
                   
                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.TorusHandleRadius1, handleColor1);
                    Handles.DrawSolidDisc(torusEffector.positionCenterRadiusHandle1, normal, handleRadius);

                    Handles.color = handleColor1;
                    Handles.DrawDottedLine(torusEffector.positionCenterOfRotation, torusEffector.positionCenterRadiusHandle1, dottedLineScreenSpaceSize);

                    Handles.color = centerColor;
                    
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine);


                    Handles.color = (selectionInfo.selectedHandleType == DCEffectorHandleType.TorusHandleDistance) ? handleSelectColor : boundaryColor;
                    if (selectionInfo.mouseOverHandleType == DCEffectorHandleType.TorusHandleDistance && selectionInfo.selectedHandleType != DCEffectorHandleType.TorusHandleDistance && selectionInfo.mouseOverEffectorIndex == selectionInfo.selectedEffectorIndex)
                    {
                        Handles.color = highLightColor;
                    }
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine + torusEffector.distanceOutward);
                    Handles.color = boundaryColor;
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine - torusEffector.distanceOutward);

                    Handles.color = boundaryColor * featherColFactor;
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine + torusEffector.distanceOutward * torusEffector.featherAmount);
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine - torusEffector.distanceOutward * torusEffector.featherAmount);

                    Handles.color = Color.white;
                    Vector3 depthPoint = new Vector3(torusEffector.positionCenterRadiusHandle1.x, torusEffector.positionCenterRadiusHandle1.y, torusEffector.depthStrength);
                    DCEditorHandleDrawFunctions.DrawArrowXZ(torusEffector.positionCenterRadiusHandle1, depthPoint, 1);
                }
                else
                {
                    bool isEfffectorMousedOver = i == selectionInfo.mouseOverEffectorIndex && selectionInfo.mouseOverEffectorType == DCEffectorType.Torus;
                    
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.TorusHandleCenter, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(torusEffector.positionCenterOfRotation, normal, handleRadius);
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.TorusHandleRadius1, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(torusEffector.positionCenterRadiusHandle1, normal, handleRadius);
                    Handles.color = deselectColor;
                    Handles.DrawDottedLine(torusEffector.positionCenterOfRotation, torusEffector.positionCenterRadiusHandle1, dottedLineScreenSpaceSize);


                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine);
                    
                    //Handles.color = (isEfffectorMousedOver && selectionInfo.mouseOverHandleType == DCEffectorHandleType.TorusHandleDistance) ? highLightColor : deselectColor;
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.TorusHandleDistance, isEfffectorMousedOver);
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine + torusEffector.distanceOutward);
                    Handles.color = deselectColor;
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine - torusEffector.distanceOutward);

                    Handles.color = deselectColor * featherColFactor;
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine + torusEffector.distanceOutward * torusEffector.featherAmount);
                    Handles.DrawWireArc(torusEffector.positionCenterOfRotation, normal, torusEffector.positionCenterRadiusHandle1, 360, torusEffector.RadiusAtCenterLine - torusEffector.distanceOutward * torusEffector.featherAmount);

                }


                
            }
        }


        void DrawSemiTorusEffectors()
        {
            List<DCSemiTorusEffector> semiTorusEffectorList = effectorManager.semiTorusEffectorList;
            for (int i = 0; i < semiTorusEffectorList.Count; i++)
            {
                Handles.color = handleColor1;
                DCSemiTorusEffector semiTorusEffector = semiTorusEffectorList[i];
                float radiusCenterLine = semiTorusEffector.RadiusAtCenterLine;

                if (selectionInfo.selectedEffectorType == DCEffectorType.SemiTorus && i == selectionInfo.selectedEffectorIndex)
                {
                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.TorusHandleCenter, handleColor1);
                    Handles.DrawSolidDisc(semiTorusEffector.positionCenterOfRotation, normal, handleRadius);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.TorusHandleRadius1, handleColor1);
                    Handles.DrawSolidDisc(semiTorusEffector.positionCenterRadiusHandle1, normal, handleRadius);
                    Handles.DrawDottedLine(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, dottedLineScreenSpaceSize);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.TorusHandleRadius2, handleColor2);
                    Handles.DrawSolidDisc(semiTorusEffector.positionCenterRadiusHandle2, normal, handleRadius);
                    Handles.DrawDottedLine(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle2, dottedLineScreenSpaceSize);

                    Handles.color = centerColor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine, true);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.TorusHandleDistance, boundaryColor);
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine + semiTorusEffector.distanceOutward, true);

                    Handles.color = boundaryColor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward, true);
                    Handles.color = boundaryColor * featherColFactor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine + semiTorusEffector.distanceOutward * semiTorusEffector.featherAmount, true);
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward * semiTorusEffector.featherAmount, true);

                    if (semiTorusEffector.useStartAndEndCaps)
                    {
                        semiTorusEffector.UpdateEffector(); // force update for proper circle effector values
                        DCSemiCircleEffector semiCircleEffector1 = semiTorusEffector.semiCircleEffectorStart;
                        DCSemiCircleEffector semiCircleEffector2 = semiTorusEffector.semiCircleEffectorEnd;

                        Handles.color = boundaryColor;
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector1.positionCenter, semiCircleEffector1.positionRadiusHandle1, semiCircleEffector1.positionRadiusHandle2, semiTorusEffector.distanceOutward, true);
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector2.positionCenter, semiCircleEffector2.positionRadiusHandle1, semiCircleEffector2.positionRadiusHandle2, semiTorusEffector.distanceOutward, true);

                        Handles.color = boundaryColor * featherColFactor;
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector1.positionCenter, semiCircleEffector1.positionRadiusHandle1, semiCircleEffector1.positionRadiusHandle2, semiTorusEffector.distanceOutward * semiCircleEffector1.featherAmount, true);
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector2.positionCenter, semiCircleEffector2.positionRadiusHandle1, semiCircleEffector2.positionRadiusHandle2, semiTorusEffector.distanceOutward * semiCircleEffector1.featherAmount, true);
                    }

                    Handles.color = Color.white;
                    Vector3 depthPoint = new Vector3(semiTorusEffector.positionCenterRadiusHandle1.x, semiTorusEffector.positionCenterRadiusHandle1.y, semiTorusEffector.depthStrength);
                    DCEditorHandleDrawFunctions.DrawArrowXZ(semiTorusEffector.positionCenterRadiusHandle1, depthPoint, 1);
                }
                else
                {
                    bool isEfffectorMousedOver = i == selectionInfo.mouseOverEffectorIndex && selectionInfo.mouseOverEffectorType == DCEffectorType.SemiTorus;                    
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.TorusHandleCenter, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(semiTorusEffector.positionCenterOfRotation, normal, handleRadius);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.TorusHandleRadius1, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(semiTorusEffector.positionCenterRadiusHandle1, normal, handleRadius);
                    Handles.DrawDottedLine(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, dottedLineScreenSpaceSize);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.TorusHandleRadius2, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(semiTorusEffector.positionCenterRadiusHandle2, normal, handleRadius);
                    Handles.DrawDottedLine(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle2, dottedLineScreenSpaceSize);

                    Handles.color = deselectColor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine, true);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.TorusHandleDistance, isEfffectorMousedOver);                    
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine + semiTorusEffector.distanceOutward, true);

                    Handles.color = deselectColor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward, true);
                    
                    if (semiTorusEffector.useStartAndEndCaps)
                    {
                        semiTorusEffector.UpdateEffector(); // force update for proper circle effector values
                        DCSemiCircleEffector semiCircleEffector1 = semiTorusEffector.semiCircleEffectorStart;
                        DCSemiCircleEffector semiCircleEffector2 = semiTorusEffector.semiCircleEffectorEnd;

                        Handles.color = deselectColor;
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector1.positionCenter, semiCircleEffector1.positionRadiusHandle1, semiCircleEffector1.positionRadiusHandle2, semiTorusEffector.distanceOutward, true);
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector2.positionCenter, semiCircleEffector2.positionRadiusHandle1, semiCircleEffector2.positionRadiusHandle2, semiTorusEffector.distanceOutward, true);

                        Handles.color = deselectColor * featherColFactor;
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector1.positionCenter, semiCircleEffector1.positionRadiusHandle1, semiCircleEffector1.positionRadiusHandle2, semiTorusEffector.distanceOutward * semiCircleEffector1.featherAmount, true);
                        DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector2.positionCenter, semiCircleEffector2.positionRadiusHandle1, semiCircleEffector2.positionRadiusHandle2, semiTorusEffector.distanceOutward * semiCircleEffector1.featherAmount, true);
                    }

                    Handles.color = deselectColor * featherColFactor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine + semiTorusEffector.distanceOutward * semiTorusEffector.featherAmount, true);
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward * semiTorusEffector.featherAmount, true);

                }
            }
        }

        void DrawBoxEffectors()
        {
            List<DCBoxEffector> boxEffectorList = effectorManager.boxEffectorList;
            for (int i = 0; i < boxEffectorList.Count; i++)
            {
                
                DCBoxEffector boxEffector = boxEffectorList[i];
                Vector2 feather1A = (boxEffector.positionDistanceHandle1A - boxEffector.position1) * boxEffector.featherAmount + boxEffector.position1;
                Vector2 feather1B = (boxEffector.positionDistanceHandle1B - boxEffector.position1) * boxEffector.featherAmount + boxEffector.position1;
                Vector2 feather2A = (boxEffector.positionDistanceHandle2A - boxEffector.position2) * boxEffector.featherAmount + boxEffector.position2;
                Vector2 feather2B = (boxEffector.positionDistanceHandle2B - boxEffector.position2) * boxEffector.featherAmount + boxEffector.position2;

                if (selectionInfo.selectedEffectorType == DCEffectorType.Box && i == selectionInfo.selectedEffectorIndex)
                {
                    Handles.color = centerColor;
                    Handles.DrawLine(boxEffector.position1, boxEffector.position2);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandlePosition1, handleColor1);
                    Handles.DrawSolidDisc(boxEffector.position1, normal, handleRadius);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandlePosition2, new Color(0.6f,0.85f,0.95f,1));
                    Handles.DrawSolidDisc(boxEffector.position2, normal, handleRadius);

                    
                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance1A, handleColor2);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle1A, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle1A, boxEffector.position1, dottedLineScreenSpaceSize);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance1B, handleColor2);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle1B, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle1B, boxEffector.position1, dottedLineScreenSpaceSize);


                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance2A, handleColor2);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle2A, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle2A, boxEffector.position2, dottedLineScreenSpaceSize);

                    Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance2B, handleColor2);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle2B, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle2B, boxEffector.position2, dottedLineScreenSpaceSize);

                    Handles.color = boundaryColor;
                    Handles.DrawLine(boxEffector.positionDistanceHandle1A, boxEffector.positionDistanceHandle2A);
                    Handles.DrawLine(boxEffector.positionDistanceHandle1B, boxEffector.positionDistanceHandle2B);

                    Handles.color = boundaryColor * featherColFactor;

                    Handles.DrawLine(feather1A, feather2A);
                    Handles.DrawLine(feather1B, feather2B);

                    Handles.color = Color.white;
                    Vector3 depthPoint1 = new Vector3(boxEffector.position1.x, boxEffector.position1.y, boxEffector.depthStrength1);
                    DCEditorHandleDrawFunctions.DrawArrowXZ(boxEffector.position1, depthPoint1, 1);

                    Handles.color = Color.white;
                    Vector3 depthPoint2 = new Vector3(boxEffector.position2.x, boxEffector.position2.y, boxEffector.depthStrength2);
                    DCEditorHandleDrawFunctions.DrawArrowXZ(boxEffector.position2, depthPoint2, 1);

                    Handles.DrawLine(depthPoint1, depthPoint2);


                }
                else
                {
                    bool isEfffectorMousedOver = i == selectionInfo.mouseOverEffectorIndex && selectionInfo.mouseOverEffectorType == DCEffectorType.Box;
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.BoxHandlePosition1, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(boxEffector.position1, normal, handleRadius);
                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.BoxHandlePosition2, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(boxEffector.position2, normal, handleRadius);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.BoxHandleDistance1A, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle1A, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle1A, boxEffector.position1, dottedLineScreenSpaceSize);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.BoxHandleDistance1B, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle1B, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle1B, boxEffector.position1, dottedLineScreenSpaceSize);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.BoxHandleDistance2A, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle2A, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle2A, boxEffector.position2, dottedLineScreenSpaceSize);

                    Handles.color = ColorForDeselectedHandle(DCEffectorHandleType.BoxHandleDistance2B, isEfffectorMousedOver);
                    Handles.DrawSolidDisc(boxEffector.positionDistanceHandle2B, normal, handleRadius);
                    Handles.DrawDottedLine(boxEffector.positionDistanceHandle2B, boxEffector.position2, dottedLineScreenSpaceSize);

                    Handles.color = deselectColor;
                    Handles.DrawLine(boxEffector.position1, boxEffector.position2);
                    Handles.DrawLine(boxEffector.positionDistanceHandle1A, boxEffector.positionDistanceHandle2A);
                    Handles.DrawLine(boxEffector.positionDistanceHandle1B, boxEffector.positionDistanceHandle2B);

                    Handles.color = deselectColor * featherColFactor;

                    Handles.DrawLine(feather1A, feather2A);
                    Handles.DrawLine(feather1B, feather2B);
                }                
            }
        }

        void DrawMultiEffectors()
        {
            
            List<DCMultiEffector> multiEffectorList = effectorManager.multiEffectorList;
            for (int i = 0; i < multiEffectorList.Count; i++)
            {
                DCMultiEffector multiEffector = multiEffectorList[i];

                bool multiEffectorIsSelected = (selectionInfo.selectedEffectorType == DCEffectorType.Multi && i == selectionInfo.selectedEffectorIndex);
                bool isEfffectorMousedOver = i == selectionInfo.mouseOverEffectorIndex && selectionInfo.mouseOverEffectorType == DCEffectorType.Multi;

                List<DCBoxEffector> boxEffectors = multiEffector.boxEffectorList;
                for (int j = 0; j < boxEffectors.Count; j++)
                {
                    DrawMultiBoxEffector(boxEffectors[j], multiEffectorIsSelected);
                }

                List<DCInterpSemiTorusEffector> semiTorusEffectors = multiEffector.semiTorusEffectorList;
                for (int j = 0; j < semiTorusEffectors.Count; j++)
                {
                    DrawInterpSemiTorusEffector(semiTorusEffectors[j], multiEffectorIsSelected);
                }

                if (multiEffector.useStartAndEndCaps)
                {

                    DCSemiCircleEffector semiCircleEffector1 = multiEffector.startAndEndCapEffectors[0];
                    DCSemiCircleEffector semiCircleEffector2 = multiEffector.startAndEndCapEffectors[1];

                    Handles.color = multiEffectorIsSelected ? boundaryColor : deselectColor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector1.positionCenter, semiCircleEffector1.positionRadiusHandle1, semiCircleEffector1.positionRadiusHandle2, semiCircleEffector1.Radius, true);
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector2.positionCenter, semiCircleEffector2.positionRadiusHandle1, semiCircleEffector2.positionRadiusHandle2, semiCircleEffector2.Radius, true);

                    Handles.color = Handles.color * featherColFactor;
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector1.positionCenter, semiCircleEffector1.positionRadiusHandle1, semiCircleEffector1.positionRadiusHandle2, semiCircleEffector1.Radius * semiCircleEffector1.featherAmount, true);
                    DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiCircleEffector2.positionCenter, semiCircleEffector2.positionRadiusHandle1, semiCircleEffector2.positionRadiusHandle2, semiCircleEffector2.Radius * semiCircleEffector1.featherAmount, true);
                }

                DrawMultiEffectorHandles(multiEffector, multiEffectorIsSelected, isEfffectorMousedOver);
            }
        }


        void DrawMultiBoxEffector(DCBoxEffector boxEffector, bool isSelected)
        {

            Vector2 feather1A = (boxEffector.positionDistanceHandle1A - boxEffector.position1) * boxEffector.featherAmount + boxEffector.position1;
            Vector2 feather1B = (boxEffector.positionDistanceHandle1B - boxEffector.position1) * boxEffector.featherAmount + boxEffector.position1;
            Vector2 feather2A = (boxEffector.positionDistanceHandle2A - boxEffector.position2) * boxEffector.featherAmount + boxEffector.position2;
            Vector2 feather2B = (boxEffector.positionDistanceHandle2B - boxEffector.position2) * boxEffector.featherAmount + boxEffector.position2;
            if ((boxEffector.position1 - boxEffector.position2).sqrMagnitude < eps)
            {
                return;
            }
            if (isSelected)
            {
                //Handles.color = centerColor;
                //Handles.DrawLine(boxEffector.position1, boxEffector.position2);

                /*
                Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandlePosition1, handleColor1);
                Handles.DrawSolidDisc(boxEffector.position1, normal, handleRadius);

                Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandlePosition2, handleColor1);
                Handles.DrawSolidDisc(boxEffector.position2, normal, handleRadius);
                */

                //Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance1A, handleColor2);
                Handles.color = handleColor2;
                //Handles.DrawSolidDisc(boxEffector.positionDistanceHandle1A, normal, handleRadius);
                Handles.DrawDottedLine(boxEffector.positionDistanceHandle1A, boxEffector.position1, dottedLineScreenSpaceSize);

                //Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance1B, handleColor2);
                //Handles.DrawSolidDisc(boxEffector.positionDistanceHandle1B, normal, handleRadius);
                Handles.DrawDottedLine(boxEffector.positionDistanceHandle1B, boxEffector.position1, dottedLineScreenSpaceSize);


                //Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance2A, handleColor2);
                //Handles.DrawSolidDisc(boxEffector.positionDistanceHandle2A, normal, handleRadius);
                Handles.DrawDottedLine(boxEffector.positionDistanceHandle2A, boxEffector.position2, dottedLineScreenSpaceSize);

                //Handles.color = ColorSelectForHandle(DCEffectorHandleType.BoxHandleDistance2B, handleColor2);
                //Handles.DrawSolidDisc(boxEffector.positionDistanceHandle2B, normal, handleRadius);
                Handles.DrawDottedLine(boxEffector.positionDistanceHandle2B, boxEffector.position2, dottedLineScreenSpaceSize);

                Handles.color = boundaryColor;
                Handles.DrawLine(boxEffector.positionDistanceHandle1A, boxEffector.positionDistanceHandle2A);
                Handles.DrawLine(boxEffector.positionDistanceHandle1B, boxEffector.positionDistanceHandle2B);

                Handles.color = boundaryColor * featherColFactor;

                Handles.DrawLine(feather1A, feather2A);
                Handles.DrawLine(feather1B, feather2B);

                Handles.color = Color.white;
                Vector3 depthPoint1 = new Vector3(boxEffector.position1.x, boxEffector.position1.y, boxEffector.depthStrength1);
                DCEditorHandleDrawFunctions.DrawArrowXZ(boxEffector.position1, depthPoint1, 1);

                Handles.color = Color.white;
                Vector3 depthPoint2 = new Vector3(boxEffector.position2.x, boxEffector.position2.y, boxEffector.depthStrength2);
                DCEditorHandleDrawFunctions.DrawArrowXZ(boxEffector.position2, depthPoint2, 1);

                Handles.DrawLine(depthPoint1, depthPoint2);
            }
            else
            {
                Handles.color = deselectColor;
                Handles.DrawLine(boxEffector.position1, boxEffector.position2);

                Handles.DrawDottedLine(boxEffector.positionDistanceHandle1A, boxEffector.position1, dottedLineScreenSpaceSize);

                Handles.DrawDottedLine(boxEffector.positionDistanceHandle1B, boxEffector.position1, dottedLineScreenSpaceSize);


                Handles.DrawDottedLine(boxEffector.positionDistanceHandle2A, boxEffector.position2, dottedLineScreenSpaceSize);

                Handles.DrawDottedLine(boxEffector.positionDistanceHandle2B, boxEffector.position2, dottedLineScreenSpaceSize);

                Handles.DrawLine(boxEffector.positionDistanceHandle1A, boxEffector.positionDistanceHandle2A);
                Handles.DrawLine(boxEffector.positionDistanceHandle1B, boxEffector.positionDistanceHandle2B);

                Handles.color = deselectColor * featherColFactor;

                Handles.DrawLine(feather1A, feather2A);
                Handles.DrawLine(feather1B, feather2B);
            }
        }


        void DrawInterpSemiTorusEffector(DCInterpSemiTorusEffector semiTorusEffector, bool isSelected)
        {

            float radiusCenterLine = semiTorusEffector.RadiusAtCenterLine;
            if (isSelected)
            {                
                Handles.color = centerColor;
                DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine, true);
                Handles.color = boundaryColor;
                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2,
                                                    radiusCenterLine + semiTorusEffector.distanceOutward1, radiusCenterLine + semiTorusEffector.distanceOutward2, true);

                
                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward1, radiusCenterLine - semiTorusEffector.distanceOutward2, true);
                Handles.color = boundaryColor * featherColFactor;
                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine + semiTorusEffector.distanceOutward1 * semiTorusEffector.featherAmount, radiusCenterLine + semiTorusEffector.distanceOutward2 * semiTorusEffector.featherAmount, true);
                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward1 * semiTorusEffector.featherAmount, radiusCenterLine - semiTorusEffector.distanceOutward2 * semiTorusEffector.featherAmount, true);

            }
            else
            {
                Handles.color = deselectColor;
                DCEditorHandleDrawFunctions.DrawSemiCircleXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine, true);

                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2,
                                                    radiusCenterLine + semiTorusEffector.distanceOutward1, radiusCenterLine + semiTorusEffector.distanceOutward2, true);

                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward1, radiusCenterLine - semiTorusEffector.distanceOutward2, true);

                Handles.color = deselectColor * featherColFactor;
                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine + semiTorusEffector.distanceOutward1 * semiTorusEffector.featherAmount, radiusCenterLine + semiTorusEffector.distanceOutward2 * semiTorusEffector.featherAmount, true);
                DCEditorHandleDrawFunctions.DrawVaryingArcXY(semiTorusEffector.positionCenterOfRotation, semiTorusEffector.positionCenterRadiusHandle1, semiTorusEffector.positionCenterRadiusHandle2, radiusCenterLine - semiTorusEffector.distanceOutward1 * semiTorusEffector.featherAmount, radiusCenterLine - semiTorusEffector.distanceOutward2 * semiTorusEffector.featherAmount, true);
            }

        }


        void DrawMultiEffectorHandles(DCMultiEffector multiEffector, bool isSelected, bool isEfffectorMousedOver)
        {
            List<DCMultiEffectorNodeData> pathDataList = multiEffector.pathDataList;
            
            for (int i = 0; i < pathDataList.Count; i++)
            {
                DCMultiEffectorNodeData nodeData = pathDataList[i];
                if (isSelected)
                {
                    Handles.color = ColorSelectForMultiPointHandle(DCEffectorHandleType.MultiPointHandle, handleColor1, i);
                    Handles.DrawSolidDisc(nodeData.point, normal, handleRadius);

                    if (GetMultiEffectorPivotHandle(i, nodeData, out Vector2 pivotHandle))
                    {
                        Handles.color = ColorSelectForMultiPointHandle(DCEffectorHandleType.MultiRadiusPivotHandle, handleColor3, i);
                        Handles.DrawSolidDisc(pivotHandle, normal, handleRadius);
                        Handles.DrawDottedLine(pivotHandle, nodeData.point, dottedLineScreenSpaceSize);

                        Vector2 currentPivotPoint = nodeData.bisector * nodeData.currentDistancePivot + nodeData.point;
                        Vector2 perpToCurrentPivot = new Vector2(nodeData.bisector.y, -nodeData.bisector.x) * 2 * handleRadius;

                        Handles.color = handleColor3;
                        Handles.DrawSolidDisc(currentPivotPoint, normal, handleRadius * 0.5f);
                        Handles.DrawLine(currentPivotPoint + perpToCurrentPivot, currentPivotPoint - perpToCurrentPivot);


                    }

                    if (GetMultiEffectorOutwardDistanceHandle(i, pathDataList, out Vector2 distanceHandle))
                    {
                        Handles.color = ColorSelectForMultiPointHandle(DCEffectorHandleType.MultiOutwardDistanceHandle, handleColor2, i);
                        Handles.DrawSolidDisc(distanceHandle, normal, handleRadius);
                        Handles.DrawDottedLine(distanceHandle, nodeData.point, dottedLineScreenSpaceSize);

                        Vector2 currentDistanceDir = (distanceHandle - nodeData.point).normalized;
                        Vector2 perpToCurrentDistance = new Vector2(currentDistanceDir.y, -currentDistanceDir.x) * 2 * handleRadius;
                        
                        Vector2 currentDistancePoint;   // visual indication for the current distance at the selected node
                        if (!multiEffector.useAsLoop)
                        {
                            if (i == 0 && pathDataList.Count > 1)
                            {
                                Vector2 p1 = nodeData.point;
                                Vector2 p2 = pathDataList[i + 1].point;
                                Vector2 normal = new Vector2(p1.y - p2.y, p2.x - p1.x);
                                currentDistancePoint = p1 + (distanceHandle - p1).normalized * multiEffector.boxEffectorList[i].distance1;   // visual distance is equal to the boxEffector distance handle1A
                            }
                            else if (i == pathDataList.Count - 1)
                            {
                                Vector2 p1 = nodeData.point;
                                currentDistancePoint = p1 + (distanceHandle - p1).normalized * multiEffector.boxEffectorList[multiEffector.boxEffectorList.Count-1].distance2;    // visual distance is equal to the boxEffector distance handle2A
                            }
                            else
                            {
                                // average distance between interp torus start and end is the distance at the bisector
                                currentDistancePoint = currentDistanceDir * (multiEffector.semiTorusEffectorList[i].distanceOutward1 + multiEffector.semiTorusEffectorList[i].distanceOutward2) / 2 + nodeData.point;
                            }

                        }
                        else
                        {
                            // average distance between interp torus start and end is the distance at the bisector
                            currentDistancePoint = currentDistanceDir * (multiEffector.semiTorusEffectorList[i].distanceOutward1 + multiEffector.semiTorusEffectorList[i].distanceOutward2) / 2 + nodeData.point;
                        }

                        
                        // set the visual of the current node distance
                        Handles.color = handleColor2;
                        Handles.DrawSolidDisc(currentDistancePoint, normal, handleRadius * 0.5f);
                        Handles.DrawLine(currentDistancePoint + perpToCurrentDistance, currentDistancePoint - perpToCurrentDistance);
                    }

                    if (!multiEffector.useAsLoop )
                    {
                        if (i < pathDataList.Count - 1)
                        {
                            Handles.color = (selectionInfo.multiEffectorLineIndex == i) ? highLightColor : centerColor;
                            Handles.DrawDottedLine(nodeData.point, pathDataList[i + 1].point, dottedLineScreenSpaceSize);
                        }
                    }
                    else
                    {
                        Handles.color = (selectionInfo.multiEffectorLineIndex == i) ? highLightColor : centerColor;
                        Handles.DrawDottedLine(nodeData.point, pathDataList[(i + 1) % pathDataList.Count].point, dottedLineScreenSpaceSize);
                    }
                }
                else
                {
                    Handles.color = ColorDeSelectForMultiPointHandle(DCEffectorHandleType.MultiPointHandle, isEfffectorMousedOver, i);
                    Handles.DrawSolidDisc(nodeData.point, normal, handleRadius);
                    if (!multiEffector.useAsLoop)
                    {
                        if (i < pathDataList.Count - 1)
                        {
                            Handles.DrawDottedLine(nodeData.point, pathDataList[i + 1].point, dottedLineScreenSpaceSize);
                        }
                    }
                    else
                    {
                        Handles.DrawDottedLine(nodeData.point, pathDataList[(i + 1) % pathDataList.Count].point, dottedLineScreenSpaceSize);
                    }
                }
            }

        }




        void DrawBoundingBoxes()
        {
            DCEffector effector;
            Handles.color = Color.yellow;
            Vector2 topR, botL, topL, botR;

            effector = GetSelectedEffector();
            if (effector == null) return;

            topR = effector.GetBoundsTopRight();
            botL = effector.GetBoundsBottemLeft();
            topL = new Vector2(botL.x, topR.y);
            botR = new Vector2(topR.x, botL.y);

            Handles.DrawLine(topR, botR);
            Handles.DrawLine(botR, botL);
            Handles.DrawLine(botL, topL);
            Handles.DrawLine(topL, topR);           
        }


        /// <summary>
        /// Draws the preview camera based on a render texture and material for proper rendering the clearFlags of the main camera
        /// </summary>
        void DrawPreviewCamera()
        {
            if (usePreviewCamera)
            {
                if (effectorCameraGameObject == null)
                {
                    return;
                }

                // setup the texture for copying the render texture color
                if (texture == null)
                {
                    texture = new Texture2D(renderTexSize, renderTexSize, TextureFormat.RGB24, false);
                    texture.wrapMode = TextureWrapMode.Clamp;
                }
                
                int width = Screen.width;
                int height = Screen.height;

                //float ratioHW = (float)height / (float)width;
                float ratioWH = (float)width / (float)height;

                Camera cam = effectorCameraGameObject.GetComponent<Camera>();

                float baseWidthSize = 0.25f;            // fill bottom right screne 25%
                //float screenRatio1080 = 0.5265f;        // h/w ratio of standard screen to make the preview camera this scale
                float scale = baseWidthSize * camPreviewSizeFactor;
                
                cam.rect = new Rect(0, 0, 1, 1);    // square rect ful

                // render a black underlayer before rendering the camera
                if (!previewCamMat)
                {
                    Shader shader = Shader.Find("Unlit/Texture");
                    previewCamMat = new Material(shader);
                    previewCamMat.hideFlags = HideFlags.HideAndDontSave;

                    previewCamMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    previewCamMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                    // Turn off backface culling, depth writes, depth test.
                    previewCamMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    previewCamMat.SetInt("_ZWrite", 0);
                    previewCamMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    //previewCamMat.SetColor("_Color", Color.magenta);
                    previewCamMat.renderQueue = (int)RenderQueue.Overlay;                    
                    previewCamMat.mainTexture = texture;
                }


                GL.PushMatrix();
                previewCamMat.SetPass(0);   // set material
                GL.LoadOrtho();             // could have used GL.LoadPixelMatrix() instead. In case it is used, also update the vertices by screen size


                // make a quad in the bottom right corner
                GL.Begin(GL.QUADS);
                {
                    GL.TexCoord(new Vector3(0, 0, 0));
                    GL.Vertex(new Vector3(1 - scale*1.1f, 0, 1));

                    GL.TexCoord(new Vector3(0, 1, 0));
                    GL.Vertex(new Vector3(1 - scale * 1.1f, scale * ratioWH * 1.1f, 1));

                    GL.TexCoord(new Vector3(1, 1, 0));
                    GL.Vertex(new Vector3(1f, scale * ratioWH * 1.1f, 1));

                    GL.TexCoord(new Vector3(1, 0, 0));
                    GL.Vertex(new Vector3(1f, 0, 1));                    
                }
                GL.End();

                GL.PopMatrix();
                
                
                cam.Render();   // renders to the specified render texture

                RenderTexture oldActive = RenderTexture.active;     // old active render texture so we can set it back after reading the camera render texture
                RenderTexture.active = cam.targetTexture;
                texture.ReadPixels(new Rect(0, 0, renderTexSize, renderTexSize), 0, 0);
                texture.Apply();
                RenderTexture.active = oldActive;
            }
        }

        /// <summary>
        /// Draws the preview camera with a skybox clearFlag else it renders it additive for some reason
        /// </summary>
        void DrawPreviewCameraSimple()
        {
            if (usePreviewCamera)
            {
                if (effectorCameraGameObject == null)
                {
                    return;
                }

                int width = Screen.width;
                int height = Screen.height;

                //float ratioHW = (float)height / (float)width;
                float ratioWH = (float)width / (float)height;

                Camera cam = effectorCameraGameObject.GetComponent<Camera>();

                float baseWidthSize = 0.25f;                // fill bottom right screne 25%
                //float screenRatio1080p = 0.5265f;          // h/w ratio of standard screen to make the preview camera this scale
                float scale = baseWidthSize * camPreviewSizeFactor;
                cam.rect = new Rect(1 - scale, 0.0f, scale, scale * ratioWH);              

                // Render the camera to the current screen
                RenderTexture temp = cam.targetTexture;                
                cam.targetTexture = RenderTexture.active;   // use the active texture to render this camera
                cam.clearFlags = CameraClearFlags.Skybox;   // this is the only flag that prevents blending the background
                cam.Render();
                cam.targetTexture = temp;
            }
        }


        void SetupPreviewCameraSettings()
        {
            if (usePreviewCamera)
            {
                if (effectorManager.transform.childCount > 0)
                {
                    effectorCameraGameObject = effectorManager.transform.GetChild(0).gameObject;
                }


                if (effectorCameraGameObject == null)
                {
                    effectorCameraGameObject = new GameObject(previewCamName);
                    effectorCameraGameObject.SetActive(false);                                  // dont want to see the frustrum
                    effectorCameraGameObject.transform.SetParent(effectorManager.transform);    // parenting makes it an easy grab to the preview camera
                    
                    effectorCameraGameObject.hideFlags = HideFlags.HideAndDontSave;             // dont want to save it nor show it 
                    effectorCameraGameObject.AddComponent<Camera>();

                    RenderTexture rTexture = new RenderTexture(renderTexSize, renderTexSize, 24);   // setup render texture
                    rTexture.name = "previewTexture";
                    rTexture.format = RenderTextureFormat.ARGB32;

                    effectorCameraGameObject.GetComponent<Camera>().CopyFrom(Camera.main);
                    effectorCameraGameObject.GetComponent<Camera>().targetTexture = rTexture;
                    effectorCameraPosZ = effectorCameraGameObject.transform.position.z;
                    effectorCameraOrthoSize = Camera.main.orthographicSize;
                }
                else
                {
                    RenderTexture temp = effectorCameraGameObject.GetComponent<Camera>().targetTexture;
                    effectorCameraGameObject.GetComponent<Camera>().CopyFrom(Camera.main);  // copy the main camera properties and transform
                    effectorCameraGameObject.GetComponent<Camera>().targetTexture = temp;  
                    effectorCameraPosZ = effectorCameraGameObject.transform.position.z;
                    effectorCameraOrthoSize = Camera.main.orthographicSize;
                }
                
            }
        }

        /// <summary>
        /// Checks if the current path point index is equal to the selected one, if it is returns a valid pivotHandle as long as the bisector exsists
        /// </summary>
        /// <param name="pathPointIndex"></param>
        /// <param name="nodeData"></param>
        /// <param name="pivotHandle"></param>
        /// <returns></returns>
        private bool GetMultiEffectorPivotHandle(int pathPointIndex, DCMultiEffectorNodeData nodeData, out Vector2 pivotHandle)
        {
            
            if (selectionInfo.selectedMultiEffectorPointIndex == pathPointIndex)
            {
                Vector2 bisector = nodeData.bisector;
                if (bisector.sqrMagnitude != 0)
                {
                    pivotHandle = nodeData.point + bisector * nodeData.desiredDistancePivot;
                    return true;
                }
            }
            pivotHandle = Vector2.zero;
            return false;
               
        }


        /// <summary>
        /// Checks if the current path point index is equal to the selected one, if it is returns a valid distance handle
        /// </summary>
        /// <param name="pathPointIndex"></param>
        /// <param name="pathDataList"></param>
        /// <param name="distanceHandle"></param>
        /// <returns></returns>
        private bool GetMultiEffectorOutwardDistanceHandle(int pathPointIndex, List<DCMultiEffectorNodeData> pathDataList, out Vector2 distanceHandle)
        {
            if (selectionInfo.selectedMultiEffectorPointIndex == pathPointIndex)
            {
                DCMultiEffectorNodeData nodeData = pathDataList[pathPointIndex];
                Vector2 bisector = nodeData.bisector;
                if (bisector.sqrMagnitude != 0)
                {
                    distanceHandle = nodeData.point - bisector * nodeData.desiredDistanceOutwards;
                    return true;
                }
                else
                {
                    if (pathDataList.Count > 1)
                    {
                        Vector2 p2, dP;
                        if (pathPointIndex != pathDataList.Count - 1)
                        {
                            p2 = pathDataList[pathPointIndex + 1].point;
                            dP = p2 - nodeData.point;
                        }
                        else
                        {
                            p2 = pathDataList[pathPointIndex - 1].point;
                            dP = nodeData.point - p2;
                        }
                        distanceHandle = nodeData.point  - (new Vector2(dP.y, -dP.x).normalized * nodeData.desiredDistanceOutwards);
                        return true;
                    }
                }                
            }
            distanceHandle = Vector2.zero;
            return false;
        }
                


        private Color ColorSelectForHandle(DCEffectorHandleType handleType, Color handleColor)
        {            
            return (selectionInfo.selectedHandleType == handleType) ? handleSelectColor : (selectionInfo.mouseOverEffectorIndex == selectionInfo.selectedEffectorIndex && selectionInfo.mouseOverHandleType == handleType && selectionInfo.mouseOverEffectorType == selectionInfo.selectedEffectorType) ? highLightColor : handleColor;
        }

        private Color ColorSelectForMultiPointHandle(DCEffectorHandleType handleType, Color handleColor, int pointIndex)
        {
            Color col = handleColor;
            if (selectionInfo.selectedHandleType == handleType && pointIndex == selectionInfo.selectedMultiEffectorPointIndex)
            {
                col = handleSelectColor;
            }

            else if (pointIndex == selectionInfo.mouseOverMultiEffectorPointIndex && selectionInfo.mouseOverEffectorType == selectionInfo.selectedEffectorType && selectionInfo.mouseOverHandleType == handleType && selectionInfo.mouseOverEffectorIndex == selectionInfo.selectedEffectorIndex)
            {
                col = highLightColor;
            }

            return col;           
        }

        private Color ColorDeSelectForMultiPointHandle(DCEffectorHandleType handleType, bool isEfffectorMousedOver, int pointIndex)
        {
            Color col = deselectColor;
            if (pointIndex == selectionInfo.mouseOverMultiEffectorPointIndex && selectionInfo.mouseOverHandleType == handleType  && isEfffectorMousedOver)
            {
                col = highLightColor;
            }
            return col;
        }


        private Color ColorForDeselectedHandle(DCEffectorHandleType handleType, bool isEfffectorMousedOver)
        {
            
            return (isEfffectorMousedOver && selectionInfo.mouseOverHandleType == handleType) ? highLightColor : deselectColor;
        }


        /// <summary>
        /// Gets the square distance to the segment if the point is projected on the segment. Else the distance value = float.MaxValue
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        private float DistanceSquareOnSegmentProjected(Vector2 A, Vector2 B, Vector2 point)
        {
            Vector2 v = B - A;
            Vector2 u = point - A;

            Vector2 prjPoint = Vector2.Dot(v, u) / v.sqrMagnitude * v + A;

            if ( Vector2.Dot(prjPoint - A, v) >= 0 && Vector2.Dot(B - prjPoint, v) >= 0)
            {
                return (point - prjPoint).sqrMagnitude;
            }
            else
            {
                return float.MaxValue;
            }

        }

        


        public enum DCEffectorType
        {
            None, Circle, SemiCircle, Torus, SemiTorus, Box, Multi
        }

        public enum DCEffectorHandleType
        {
            None, CircleHandleCenter, CircleHandleRadius1, CircleHandleRadius2, TorusHandleCenter, TorusHandleDistance, TorusHandleRadius1, TorusHandleRadius2,
            BoxHandlePosition1, BoxHandlePosition2, BoxHandleDistance1A, BoxHandleDistance1B, BoxHandleDistance2A, BoxHandleDistance2B, 
            MultiPointHandle, MultiOutwardDistanceHandle, MultiRadiusPivotHandle, MultiSegmentHandle, RegionShaper
        }

        public class SelectionInfo
        {
            // Effector types
            public DCEffectorType selectedEffectorType = DCEffectorType.None;
            public DCEffectorType mouseOverEffectorType = DCEffectorType.None;            

            public int selectedEffectorIndex = -1;
            public int mouseOverEffectorIndex = -1;

            // Handle types
            public DCEffectorHandleType selectedHandleType = DCEffectorHandleType.None;
            public DCEffectorHandleType mouseOverHandleType = DCEffectorHandleType.None;

            // Indices for multi effectors
            public int selectedMultiEffectorPointIndex = -1;
            public int mouseOverMultiEffectorPointIndex = -1;

            public int multiEffectorLineIndex = -1;
            public bool mouseIsOverMultiEffectorLine;

            // Indices for region bounding box
            public int selectedRegionPointIndex = -1;
            public int mouseOverRegionPointIndex = -1;
            public int mouseOverRegionLineIndex = -1;

            // information for when we start a mouse drag
            public Vector3 positionAtStartOfDrag;           // a vector position used at the start of drag (can be anything, like offsets etc)
            public Vector3 positionHandleAtStartOfDrag;     // the effector handle position at the start of a drag
            public float valueAtStartOfDrag;                // a float value at the start of a drag
            


            public void ResetMultiPointSelectionInfo()
            {
                selectedMultiEffectorPointIndex = -1;
                mouseOverMultiEffectorPointIndex = -1;
                multiEffectorLineIndex = -1;
                mouseIsOverMultiEffectorLine = false;
            }

            public void ResetRegionSelectionInfo()
            {
                selectedRegionPointIndex = -1;
                mouseOverRegionPointIndex = -1;
                mouseOverRegionLineIndex = -1;
            }

            /// <summary>
            ///  sets all mouse over info to default values
            /// </summary>
            public void ResetMouseOverInfo()
            {
                mouseOverEffectorType = DCEffectorType.None;
                mouseOverHandleType = DCEffectorHandleType.None;

                mouseOverEffectorIndex = -1;

                multiEffectorLineIndex = -1;
                mouseIsOverMultiEffectorLine = false;
                mouseOverMultiEffectorPointIndex = -1;

                mouseOverRegionLineIndex = -1;
                mouseOverRegionPointIndex = -1;
            }
        }
    }
}

