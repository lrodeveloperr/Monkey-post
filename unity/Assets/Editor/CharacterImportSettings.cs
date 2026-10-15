using System.IO;
using UnityEditor;

namespace MonkeyPost.EditorTools
{
    /// <summary>
    /// Imports the character art without any manual Unity steps (see docs/ANIMATION_ASSETS.md):
    /// <list type="bullet">
    /// <item>Assets/Resources/Characters: the footballer model, as a Humanoid rig.</item>
    /// <item>Assets/Resources/Animations: Mixamo clips (FBX, "Without Skin"), as Humanoid so they
    /// retarget onto the model; each clip is renamed after its file; files named "Loop_*" loop;
    /// root motion is baked into the pose (the simulation moves the players).</item>
    /// </list>
    /// </summary>
    public sealed class CharacterImportSettings : AssetPostprocessor
    {
        const string CharactersFolder = "Assets/Resources/Characters/";
        const string AnimationsFolder = "Assets/Resources/Animations/";

        void OnPreprocessModel()
        {
            bool character = assetPath.StartsWith(CharactersFolder);
            bool animation = assetPath.StartsWith(AnimationsFolder);
            if (!character && !animation) return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = animation;
            if (animation)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importBlendShapes = false;
            }
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationsFolder)) return;
            var importer = (ModelImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath);
            bool loop = name.StartsWith("Loop_");

            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = clips.Length == 1 ? name : $"{name}_{i}";
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
                // In place: the simulation owns position and heading.
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
