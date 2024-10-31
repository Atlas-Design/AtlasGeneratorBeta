using UnityEngine;
using UnityEditor;
using System.IO;

public class ImageImportSettings : Editor
{
    // Add a right-click context menu for image files in a specific folder
    [MenuItem("Assets/Prepare import settings for 3D generation", true)]
    private static bool ValidateImageImportOption()
    {
        // Make sure the selected object is an image and inside the 'Assets/images/original-images' folder
        string[] selectedPaths = Selection.assetGUIDs;
        foreach (string guid in selectedPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            // Check if the selected file is inside the folder
            if (!path.StartsWith("Assets/AtlasGeneratorBeta/images/original-images") || !IsImageFile(path))
            {
                return false;
            }
        }
        return true;
    }

    // The actual menu item to modify import settings
    [MenuItem("Assets/Prepare import settings for 3D generation")]
    private static void ApplyImportSettings()
    {
        string[] selectedPaths = Selection.assetGUIDs;
        foreach (string guid in selectedPaths)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;

            if (textureImporter != null)
            {
                textureImporter.isReadable = true;  // Enable Read/Write
                textureImporter.alphaIsTransparency = true;  // Enable Alpha is Transparency
                textureImporter.textureCompression = TextureImporterCompression.Uncompressed;  // Set compression to None

                // Apply the changes
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"Import settings updated for {path}");
            }
        }
    }

    // Utility function to check if the selected file is an image
    private static bool IsImageFile(string path)
    {
        string extension = Path.GetExtension(path).ToLower();
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".tga" || extension == ".bmp";
    }
}
