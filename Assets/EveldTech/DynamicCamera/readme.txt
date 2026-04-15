Before starting 1:
The demo scenes are built around the built-in renderer of Unity.
If you have opened a demo scene in the universal RP you have to change the materials to the UniversalRP materials:

select all materials in the demo assets folder
go to: Edit > Render Pipeline > Universal Render Pipeline > Upgraded Selected Materials to UniversalRP Materials

Same goes for HDRP

Before starting 2:
You can only switch demo scenes in play mode if you have added the scenes to the build settings!


If opened in Unity 2018.4:
- Make sure the assembly of Eveld.DynamicCamera.Editor.asmdef use GUIDs is set to Eveld.DynamicCamera
- Several prefabs are not rotated correctly and some have meshes missing
- can't find scripts for the canvas in the SpeedScene, you can delete the canvas if this happens.
- Preview camera and labels in the effector editor do not work in 2018.4.


Issues:
A current known issue with the preview window of the effector tool is that the preview is darkish inside the Universal Render Pipeline.
