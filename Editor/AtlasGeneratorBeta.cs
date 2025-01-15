using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using System.IO;
using GluonGui.Dialog;
using Unity.VisualScripting;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Drawing;
using Color = UnityEngine.Color;
using System.IO.Compression;
using UnityEngine.UIElements;
using Image = System.Drawing.Image;
using System.Reflection;
using System.Linq;

public class GameObjectUtility: MonoBehaviour
{
    // Método estático para obtener todos los GameObjects activos en la escena
    public static GameObject[] GetAllGameObjects()
    {
        return FindObjectsOfType<GameObject>();
    }
}

public static class Utility
{
    public static void OverwriteFirstPixelWithRandomColor(string path)
    {
        byte[] imageData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(imageData);

        Color randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, 1f); // RGB aleatorio, alfa en 1

        texture.SetPixel(0, 0, randomColor);
        texture.Apply();

        byte[] modifiedData = texture.EncodeToPNG();
        File.WriteAllBytes(path, modifiedData);
    }

    public static string FindImagePathWithExtension(string img_path)
    {
        // Common image formats
        string[] extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".tiff" };

        // Try every extension
        foreach (string ext in extensions)
        {
            string fullPath = img_path + ext;
            if (File.Exists(fullPath))
            {
                return ext;
            }
        }

        return ".png";
    }

    public static void DeleteAsset(string path)
    {
        Uri fullPathUri = new Uri(path);
        Uri projectUri = new Uri(Application.dataPath); // Unity project "Assets" folder as base

        // Get the relative path by making the full path relative to the "Assets" folder
        string relativePath = projectUri.MakeRelativeUri(fullPathUri).ToString();
        string assetPath = relativePath.Replace("\\", "/");

        // Check if the asset exists before attempting to delete
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.Refresh(); // Refresh the asset database to reflect the changes
        }
        else
        {
            //\UnityEngine.Debug.LogWarning("Asset not found at path: " + assetPath);
        }
    }

    private static readonly object fileLock = new object();

    public static void MoveGeneratedFBX(string sourcePath, string destinationPath)
    {
        lock (fileLock)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(sourcePath, destinationPath);
        }
    }

    public static void CopyImage(string originalPath, string copyPath)
    {
        using (Image originalImage = Image.FromFile(originalPath))
        {
            // Creaye a copy in memory of the image
            using (Bitmap copy = new Bitmap(originalImage))
            {
                // Saves the copy on the specified path
                copy.Save(copyPath, originalImage.RawFormat);
            }
        }
    }

    public static void DeleteImage(string path)
    {
        try
        {
            // Verifies that the file exists before deleting
            if (File.Exists(path))
            {
                File.Delete(path);
                Console.WriteLine("The image has been deleted correctly.");
            }
            else
            {
                Console.WriteLine("The file does not exist on the specified path.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while deleting the image: {ex.Message}");
        }
    }

    public static int FindMaxQueuePosition()
    {
        int maxQueuePosition = 0;

        GameObject[] gameObjects = GameObjectUtility.GetAllGameObjects();

        foreach (GameObject obj in gameObjects)
        {
             foreach (var component in obj.GetComponents<Component>())
            {                
                var queuePositionField = 0;
                bool exist = false;
                if (component.GetType() == typeof(BillboardIcon))
                {
                    // Luego, intentamos obtener `queue_position` como campo
                    // queuePositionField = component.GetType().GetField("queue_position");
                    queuePositionField = (int)((BillboardIcon)component).GetType().GetField("queue_position").GetValue(component);  
                    exist = true;                
                }
                else if (component.GetType() == typeof(PropertyComponent))
                {
                    // Luego, intentamos obtener `queue_position` como campo
                    queuePositionField = (int)((PropertyComponent)component).GetType().GetField("queue_position").GetValue(component);
                    exist = true;
                }
                
                // if (queuePositionField != null && queuePositionField.FieldType == typeof(int))
                if (exist)
                {
                    // int queuePositionValue = (int)queuePositionField.GetValue(component);
                    // maxQueuePosition = Mathf.Max(maxQueuePosition, queuePositionValue);
                    maxQueuePosition = Mathf.Max(maxQueuePosition, queuePositionField);
                }
                
            }
        }

        return maxQueuePosition;
    }

    public static void DecreaseQueuePosition()
    {
        GameObject[] gameObjects = GameObjectUtility.GetAllGameObjects();
        foreach (GameObject obj in gameObjects)
        {
            foreach (var component in obj.GetComponents<Component>())
            {
                // Try to get `queue_position` as a field
                var queuePositionField = component.GetType().GetField("queue_position", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (queuePositionField != null && queuePositionField.FieldType == typeof(int))
                {
                    int queuePositionValue = (int)queuePositionField.GetValue(component);
                    if (queuePositionValue > 0)
                    {
                        // Decrease the value of `queue_position` by 1
                        queuePositionField.SetValue(component, queuePositionValue - 1);
                    }
                }
            }
        }
    }

    public static async Task WaitUntilQueuePositionIsOne(GameObject obj)
    {
        foreach (var component in obj.GetComponents<Component>())
            {
                // Try to get `queue_position` as a field
                var queuePositionField = component.GetType().GetField("queue_position", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (queuePositionField != null && queuePositionField.FieldType == typeof(int))
                {
                    while ((int)queuePositionField.GetValue(component) > 1)
                    {
                        // Decrease the value of `queue_position` by 1
                        await Task.Yield();
                    }
                }
            }
    }

    public static bool IsFBXFileValid(string filePath)
    {
        // Check if file exists
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogWarning($"FBX file not found: {filePath}");
            return false;
        }

        // Check file size (FBX files should be at least several KB)
        FileInfo fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < 1024) // Check if the file size is smaller than 1KB
        {
            UnityEngine.Debug.LogWarning($"FBX file is too small and may be corrupted: {filePath}");
            return false;
        }

        // (Optional) Read the file's content to check for an FBX signature
        using (var reader = new StreamReader(filePath))
        {
            string firstLine = reader.ReadLine();
            if (firstLine == null || !firstLine.Contains("FBX"))
            {
                UnityEngine.Debug.LogWarning($"FBX file header is invalid: {filePath}");
                return false;
            }
        }

        return true;
    }

    public static bool IsOBJFileValid(string filePath)
    {
        // Check if file exists
        if (!File.Exists(filePath))
        {
            UnityEngine.Debug.LogWarning($"OBJ file not found: {filePath}");
            return false;
        }

        // Check file size (OBJ files should be at least several KB)
        FileInfo fileInfo = new FileInfo(filePath);
        if (fileInfo.Length < 1024) // Check if the file size is smaller than 1KB
        {
            UnityEngine.Debug.LogWarning($"OBJ file is too small and may be corrupted: {filePath}");
            return false;
        }

        return true;
    }

    public static string GetUniqueFileName(string baseName, string extension)
    {
        string sanitizedBaseName = baseName.Replace(" ", "");
        return $"{sanitizedBaseName}{extension}";
    }

    public static bool RaycastWithFallbackToXZPlane(Ray ray, out Vector3 hitPoint)
    {
        RaycastHit hit;
        // Try to raycast against objects in the scene
        if (Physics.Raycast(ray, out hit))
        {
            // If a collision is found, return the hit point
            hitPoint = hit.point;
            return true; // Successfully hit an object
        }
        // Fallback: Calculate intersection with XZ plane (Y = 0)
        float t = -ray.origin.y / ray.direction.y;
        // If the ray is pointing upward (Y direction positive), there's no intersection
        if (t < 0)
        {
            hitPoint = Vector3.zero; // Optional fallback value if needed
            return false;
        }
        // Calculate the point of intersection with the XZ plane
        hitPoint = ray.origin + t * ray.direction;
        return false; // No object was hit, but we return the XZ plane point
    }

    public static void HandleDragAndDropEvents()
    {
        Event e = Event.current;

        // Get a ray from the camera based on the mouse position
        Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        Vector3 hitPoint;
        bool hitObject = RaycastWithFallbackToXZPlane(ray, out hitPoint);


        if (Event.current.type == EventType.Repaint)
        {
            if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is Texture2D)
            {
                //UnityEngine.Debug.Log(hitPoint);

                Handles.BeginGUI();
                Vector2 guiPosition = HandleUtility.WorldToGUIPoint(hitPoint);
                Texture2D texture = DragAndDrop.objectReferences[0] as Texture2D;
                // Display the texture preview
                if (texture != null)
                {
                    Rect textureRect = new Rect(guiPosition.x - 50, guiPosition.y - 50, 100, 100);  // Adjust size as needed
                    GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit, true);
                }
                Handles.EndGUI();
            }
        }

        // Handle when the drag is performed (object is dropped)
        if (e.type == EventType.DragPerform)
        {
            // Check if the dragged object is a Texture2D (an image)
            if (DragAndDrop.objectReferences.Length > 0 && DragAndDrop.objectReferences[0] is Texture2D)
            {
                // Accept the drag
                DragAndDrop.AcceptDrag();

                // Get the dropped texture
                Texture2D droppedTexture = DragAndDrop.objectReferences[0] as Texture2D;

                // Ensure that the raycast fallback hit a valid point (XZ plane)
                if (hitPoint != Vector3.zero)
                {
                    // Create a new GameObject at the hit point
                    GameObject newObject = new GameObject(droppedTexture.name);
                    newObject.transform.position = hitPoint;

                    // Add the BillboardIcon component and assign the dropped texture
                    var icon = newObject.AddComponent<BillboardIcon>();
                    icon.SetIcon(droppedTexture);
                    icon.billboardSize = 20.0f; // Default size, can be changed per object in the inspector
                    // Set the newly created object as selected in the editor
                    Selection.activeGameObject = newObject;
                    // Mark the event as used to avoid further processing
                    e.Use();
                }
            }
        }

    }

    public static async Task ExecuteClient3DAsync(string programPath, string texturePath, string request, string sourcePath)
    {
        // Obtener la ruta del archivo JSON activo
        string jsonFilePath = PipelineSelectorWindow.GetActivePipelineFilePath();
        
        jsonFilePath = Path.Combine(Application.dataPath.Replace("Assets",""),jsonFilePath).Replace("/","\\");
        UnityEngine.Debug.Log(jsonFilePath);
        // UnityEngine.Debug.Log("D:\\WORK\\Sandsoft_plugin\\Assets\\AtlasGeneratorBeta\\pipelines\\Unique3d_atlas_3.3_thick_API.json");
        // Crear el proceso

        // jsonFilePath = "D:\\WORK\\Sandsoft_plugin\\Assets\\AtlasGeneratorBeta\\pipelines\\Unique3d_atlas_3.3_thick_API.json";
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = programPath, // Ruta del programa (client3D.exe)
            Arguments = $"\"{texturePath}\" \"{jsonFilePath}\" \"{request}\" \"{sourcePath}\"", // Argumentos para el programa
            RedirectStandardOutput = true, // Redirige la salida estándar si es necesario
            RedirectStandardError = true,  // Redirige los errores
            UseShellExecute = false,       // Necesario para redirigir la salida
            CreateNoWindow = true          // Evita que se muestre una ventana de consola
        };

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.Start(); // Inicia el proceso

            // Leer la salida y errores de manera asíncrona
            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            // Espera de manera asincrónica sin bloquear el hilo principal de Unity
            await Task.Run(() => process.WaitForExit());

            string output = await outputTask;
            string error = await errorTask;

            if (!string.IsNullOrEmpty(error))
            {
                UnityEngine.Debug.LogError($"Error executing client3D.exe: {error}");
            }
            else
            {
                UnityEngine.Debug.Log($"client3D.exe executed successfully. Output: {output}");
            }

            Utility.DeleteImage(texturePath);
            Utility.DeleteImage(texturePath + ".meta");
            AssetDatabase.Refresh();
        }
    }

    public static async Task<bool> WaitForFileAsync(string filePath, int timeoutMilliseconds = 999999, int checkIntervalMilliseconds = 500)
    {
        DateTime startTime = DateTime.Now;

        while (!File.Exists(filePath))
        {
            // if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMilliseconds)
            // {
            //     UnityEngine.Debug.LogError("Timeout waiting for file.");
            //     return false;
            // }
            await Task.Delay(checkIntervalMilliseconds); // Wait asynchronously
        }

        // Wait for the file size to stabilize
        long lastSize = -1;
        while ((DateTime.Now - startTime).TotalMilliseconds < timeoutMilliseconds)
        {
            long currentSize = new FileInfo(filePath).Length;
            if (currentSize == lastSize) // File size hasn't changed
            {
                return true; // File is stable
            }
            lastSize = currentSize;
            await Task.Delay(checkIntervalMilliseconds); // Wait asynchronously
        }

        UnityEngine.Debug.LogError("Timeout waiting for file size stabilization.");
        return false;
    }
}


/// <summary>
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////END OF UTILITY BLOCK////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////END OF UTILITY BLOCK////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////END OF UTILITY BLOCK////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////END OF UTILITY BLOCK////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/// </summary>



[InitializeOnLoad]
public class DragAndDropTextureToScene
{
    static DragAndDropTextureToScene()
    {
        // Hook into the scene GUI to listen for drag-and-drop events
        SceneView.duringSceneGui += OnSceneGUI;

    }   

    // Handle scene drag-and-drop logic
    private static void OnSceneGUI(SceneView sceneView)
    {
        Utility.HandleDragAndDropEvents();
    }

    

}
public class ReadOnlyAttribute : PropertyAttribute
{
    // No implementation needed
}
[CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
public class ReadOnlyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        GUI.enabled = false;
        EditorGUI.PropertyField(position, property, label);
        GUI.enabled = true;
    }
}

// Custom Component to display the texture as a billboard icon in the scene
public class BillboardIcon : MonoBehaviour
{
    public Texture2D iconTexture;
    public float billboardSize = 1.0f; // Control the base size of the billboard icon
    [SerializeField]
    [ReadOnly]
    public int queue_position = 0;

    public bool IsProcessing { get; private set; } // Processing state flag
    public bool HasErrorOccurred { get; private set; }
    public DateTime? StartTime { get; private set; } // Store the start time
    public int Retries { get; private set; } // Track number of retries

    private const int maxRetries = 3;



    public void SetIcon(Texture2D texture)
    {
        iconTexture = texture;
    }   

       


    // public async void ConvertTo3D()
    // {
    //     // gameObject.name = iconTexture.name + " (Generating)";
    //     gameObject.name = iconTexture.name;
    //     // gameObject.name = iconTexture.name;
    //     IsProcessing = true;
    //     HasErrorOccurred = false;
    //     if (this.queue_position == 0)
    //     {        
    //         this.queue_position = FindMaxQueuePosition()+1;
    //         EditorUtility.SetDirty(this);

    //         StartTime = DateTime.Now;
    //         string nowString = DateTime.Now.ToString("HH-mm-ss");
    //         string uniqueFileName = gameObject.name+ "-" +nowString +".fbx";

    //         await WaitUntilQueuePositionIsOne(gameObject);

    //         if (queue_position == 1)
    //         {
    //                 // string pythonScriptPath = Path.Combine(Application.dataPath, "AtlasGeneratorBeta/Editor/client_3D.py");
    //                 string programPath = Path.Combine(Application.dataPath, "AtlasGeneratorBeta/Editor/client3D/client3D.exe");
    //                 string projectRootPath = Application.dataPath.Replace("/Assets", "");

    //                 // Use unique file names for each process
    //                 // string sourcePath = Path.Combine(projectRootPath, uniqueFileName); // Unique result file
    //                 string destinationPath = Path.Combine(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx",""), uniqueFileName);

    //                 // string original_img_path = Application.dataPath + "/AtlasGeneratorBeta/images/original-images/"+iconTexture.name+".png";
    //                 string original_img_path = Application.dataPath + "/AtlasGeneratorBeta/images/original-images/"+iconTexture.name;
    //                 original_img_path = original_img_path+FindImagePathWithExtension(original_img_path);
                                                    
    //                 string img_path = Application.dataPath + "/AtlasGeneratorBeta/images/temp-images/"+iconTexture.name + "-" +nowString + FindImagePathWithExtension(original_img_path);
    //                 CopyImage(original_img_path, img_path);
    //                 OverwriteFirstPixelWithRandomColor(img_path);

    //                 string outputPath = destinationPath.Replace("/","\\");
    //                 img_path = img_path.Replace("/","\\");

    //                 if (!Directory.Exists(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","")))
    //                 {
    //                     Directory.CreateDirectory(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx",""));
    //                 }

    //                 // UnityEngine.Debug.Log(outputPath);
    //                 // UnityEngine.Debug.Log(img_path);
    //                 await ExecuteClient3DAsync(programPath, img_path, outputPath);
                                    
    //                 // Wait for the file without blocking Unity

    //                 while(!(await WaitForFileAsync(destinationPath)))
    //                 {
    //                     AssetDatabase.Refresh();
    //                     Thread.Sleep(1000);
    //                     // throw new Exception("File did not appear in time.");
    //                 }
                                       
    //                 // Validate the FBX file before moving or importing it
    //                 while (!IsFBXFileValid(destinationPath))
    //                 {
    //                     Thread.Sleep(1000);
    //                 }                

    //                 string importerPath = "Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","/")+ uniqueFileName;
    //                 // string importerPath = destinationPath;
                        
    //                 // Get the importer for the FBX file
    //                 ModelImporter modelImporter = AssetImporter.GetAtPath(importerPath) as ModelImporter;
    //                 if (modelImporter == null)
    //                 {
    //                     UnityEngine.Debug.LogError("I was not possible to get the ModelImporter del FBX: " + importerPath);
    //                     return;
    //                 }
    //                 // Get the directory containing the FBX file
    //                 string fbxDirectory = Path.GetDirectoryName(importerPath);

    //                 // If the directory does not exist then creates one
    //                 if (!Directory.Exists(fbxDirectory))
    //                 {
    //                     Directory.CreateDirectory(fbxDirectory);
    //                 }
    //                 // Extraxt the texture and save it on the same  directory of the FBX file
    //                 modelImporter.ExtractTextures(fbxDirectory);

    //                 // Apply ModelImporter
    //                 AssetDatabase.ImportAsset(importerPath);
    //                 // UnityEngine.Debug.Log($"Texturas extraídas a: {fbxDirectory}");    
                    
    //                 gameObject.name = iconTexture.name;
    //                 // RemoveAllComponents(gameObject);

    //                 // GameObject fbxObject = AssetDatabase.LoadAssetAtPath<GameObject>(Path.Combine("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx",""), uniqueFileName));
    //                 GameObject fbxObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","/")+ uniqueFileName);
    //                 UnityEngine.Debug.Log("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","/")+ uniqueFileName);
    //                 // Verificar si se cargó correctamente
    //                 if (fbxObject != null)
    //                 {
    //                     GameObject newInstance = Instantiate(fbxObject);
    //                     newInstance.name = fbxObject.name;
    //                     newInstance.transform.position = gameObject.transform.position;
    //                     newInstance.transform.localScale = new Vector3(15f,15f,15f);
    //                     newInstance.transform.localScale *= billboardSize;
    //                     DecreaseQueuePosition();
    //                     DestroyImmediate(gameObject);                    
    //                     AssetDatabase.Refresh();                    
    //                     return;
    //                 }
                    
    //                 AssetDatabase.Refresh();

    //         }
    //     }    
    //     IsProcessing = false;
    //     // Notify the request queue that the request has been completed
    // }
    


    public async void ConvertTo3D()
    {
        // gameObject.name = iconTexture.name + " (Generating)";
        gameObject.name = iconTexture.name;
        // gameObject.name = iconTexture.name;
        IsProcessing = true;
        HasErrorOccurred = false;
        if (this.queue_position == 0)
        {        
            this.queue_position = Utility.FindMaxQueuePosition()+1;
            EditorUtility.SetDirty(this);

            StartTime = DateTime.Now;
            string nowString = DateTime.Now.ToString("HH-mm-ss");
            string uniqueFileName = gameObject.name+ "-" +nowString +".obj";

            await Utility.WaitUntilQueuePositionIsOne(gameObject);

            if (queue_position == 1)
            {
                    // string pythonScriptPath = Path.Combine(Application.dataPath, "AtlasGeneratorBeta/Editor/client_3D.py");
                    string programPath = Path.Combine(Application.dataPath, "AtlasGeneratorBeta/Editor/client3D/client3D.exe");
                    string projectRootPath = Application.dataPath.Replace("/Assets", "");

                    string destinationPath = Path.Combine(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".obj",""), uniqueFileName);
                    string original_img_path = Application.dataPath + "/AtlasGeneratorBeta/images/original-images/"+iconTexture.name;
                    original_img_path = original_img_path+Utility.FindImagePathWithExtension(original_img_path);
                                                    
                    string img_path = Application.dataPath + "/AtlasGeneratorBeta/images/temp-images/"+iconTexture.name + "-" +nowString + Utility.FindImagePathWithExtension(original_img_path);
                    Utility.CopyImage(original_img_path, img_path);
                    Utility.OverwriteFirstPixelWithRandomColor(img_path);

                    string outputPath = destinationPath.Replace("/","\\");
                    img_path = img_path.Replace("/","\\");

                    if (!Directory.Exists(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".obj","")))
                    {
                        Directory.CreateDirectory(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".obj",""));
                    }

                    await Utility.ExecuteClient3DAsync(programPath, img_path, "generate-obj", outputPath);                                    
                    // Wait for the file without blocking Unity
                    while(!(await Utility.WaitForFileAsync(destinationPath)))
                    {
                        AssetDatabase.Refresh();
                        Thread.Sleep(1000);
                    }
                                       
                    // Validate the obj file before moving or importing it
                    while (!Utility.IsOBJFileValid(destinationPath))
                    {
                        Thread.Sleep(1000);
                    }                

                    string importerPath = "Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".obj","/")+ uniqueFileName;
                        
                    string objDirectory = Path.GetDirectoryName(importerPath);
                    
                    gameObject.name = iconTexture.name;
                    AssetDatabase.Refresh();
                    // RemoveAllComponents(gameObject);

                    GameObject objObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".obj","/")+ uniqueFileName);
                    UnityEngine.Debug.Log("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".obj","/")+ uniqueFileName);
                    
                    // Verificar si se cargó correctamente
                    if (objObject != null)
                    {
                        GameObject newInstance = Instantiate(objObject);
                        newInstance.name = uniqueFileName.Replace(".obj","");
                        newInstance.transform.position = gameObject.transform.position;
                        // newInstance.transform.localScale = new Vector3(15f,15f,15f);
                        newInstance.transform.localScale *= billboardSize / 6.666f;

                        var propertyComponent = newInstance.AddComponent<PropertyComponent>();
                        propertyComponent.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AtlasGeneratorBeta/images/original-images/"+iconTexture.name + Utility.FindImagePathWithExtension(original_img_path)); // Asignar textura
                        propertyComponent.readyToTex = true;  // Establecer como listo
                        propertyComponent.creationTime = nowString;
                        Utility.DecreaseQueuePosition();
                        DestroyImmediate(gameObject);          
                        AssetDatabase.Refresh();                    
                        return;
                    }
                    
                    
                    AssetDatabase.Refresh();

            }
        }    
        IsProcessing = false;
        // Notify the request queue that the request has been completed
    }   
  
    int GetYDistanceInTexture(Texture2D tex)
    {
        int width = tex.width;
        int height = tex.height;

        int topY = -1;
        int bottomY = height + 1;

        // Iterate over all pixels in the texture
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color pixel = tex.GetPixel(x, y);
                if (pixel.a > 0.0f || pixel != Color.black) 
                {
                    if (y > topY)
                        topY = y; // Track the highest Y
                    if (y < bottomY)
                        bottomY = y; // Track the lowest Y
                }
            }
        }
        // If valid pixels were found, calculate the distance
        if (topY != -1 && bottomY != height + 1)
        {
            return topY - bottomY;
        }
        else
        {
            // No valid pixels found
            UnityEngine.Debug.LogWarning("No valid pixels found in the texture.");
            return 0;
        }
    }

    // Called when any field in the inspector is modified, ensures immediate update of gizmo
    private void OnValidate()
    {
        SceneView.RepaintAll();
    }


    private int highestY = -1; // Almacena el valor calculado de highestY
    private float previousBillboardSize = -1.0f; // Almacena el tamaño anterior

    private void OnDrawGizmos()
    {
        if (iconTexture != null)
        {
            Camera camera = SceneView.lastActiveSceneView.camera != null ? SceneView.lastActiveSceneView.camera : Camera.main;

            if (camera != null)
            {
                float distanceToCamera = Vector3.Distance(camera.transform.position, transform.position);
                float adjustedSize = billboardSize / distanceToCamera;
                Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
                Bounds objectBounds = new Bounds(transform.position, Vector3.one);
                bool isVisible = GeometryUtility.TestPlanesAABB(cameraPlanes, objectBounds);

                // Solo recalcular si el billboardSize ha cambiado
                if (billboardSize != previousBillboardSize)
                {
                    highestY = GetLowestNonTransparentY(iconTexture);
                    previousBillboardSize = billboardSize; // Actualiza el tamaño anterior
                }

                if (isVisible)
                {
                    Handles.BeginGUI();
                    Vector3 position = transform.position;
                    Vector2 guiPosition = HandleUtility.WorldToGUIPoint(position);

                    // Calcular el ratio basado en el Y más alto encontrado
                    float textureHeightRatio = 1.0f - ((float)highestY / iconTexture.height);

                    // Ajustar el rectángulo para que la imagen se dibuje sobre el pivote
                    Rect textureRect = new Rect(
                        guiPosition.x - adjustedSize * 50, // Centrado horizontalmente
                        guiPosition.y - adjustedSize * 100 * textureHeightRatio, // Ajustar el Y más alto de la imagen
                        adjustedSize * 100,
                        adjustedSize * 100
                    );

                    GUI.DrawTexture(textureRect, iconTexture);
                    Handles.EndGUI();
                    SceneView.RepaintAll();
                }
            }
        }
    }

    // Function to determine the highest Y position with non-transparent pixels (visually lowest point)
    private int GetLowestNonTransparentY(Texture2D texture, float alphaThreshold = 0.1f)
    {
        // Loop through each row from bottom to top
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                // Get the pixel color
                Color pixelColor = texture.GetPixel(x, y);

                // Check if the pixel is not transparent (alpha greater than the threshold)
                if (pixelColor.a > alphaThreshold)
                {
                    // Return the Y position of the first non-transparent row (the lowest Y)
                    return y;
                }
            }
        }

        // If no non-transparent pixels are found, return -1 (indicating fully transparent)
        return -1;
    }
}


// Custom Editor for BillboardIcon component
[CustomEditor(typeof(BillboardIcon))]
[CanEditMultipleObjects]
public class BillboardIconEditor : Editor
{

    public override void OnInspectorGUI()
    {
        // Draw the default Inspector elements
        DrawDefaultInspector();

        BillboardIcon billboardIcon = (BillboardIcon)target;

        // Add a space in the inspector
        EditorGUILayout.Space();
        // Check if the process is running
        if (billboardIcon.IsProcessing)
        {
            // Disable the button and show "Processing" label
            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Button("Convert to 3D");
            EditorGUI.EndDisabledGroup();

            // Display "Processing..." message
            EditorGUILayout.HelpBox("Processing... Please wait.", MessageType.Info);

            // Calculate and display the elapsed time
            if (billboardIcon.StartTime.HasValue)
            {
                TimeSpan elapsedTime = DateTime.Now - billboardIcon.StartTime.Value;
                EditorGUILayout.LabelField("Elapsed Time:", $"{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}");
            }

            // Display the number of retries
            // EditorGUILayout.LabelField("Retries:", billboardIcon.Retries.ToString());
            // Repaint the inspector to keep updating the UI
            Repaint();
        }
        else
        {
            // Button is enabled only when not processing
            if (GUILayout.Button("Convert to 3D"))
            {
                billboardIcon.ConvertTo3D();
            }
        }
    }
}

public class PropertyComponent : MonoBehaviour
{
    [ReadOnly] 
    public Texture2D texture;    // Propiedad para almacenar una textura
    [ReadOnly] 
    public string creationTime;    // Propiedad para almacenar una textura
    [SerializeField]
    [ReadOnly]
    public int queue_position = 0;

    [HideInInspector] 
    public bool readyToTex;      // Propiedad para indicar si está listo

    public bool IsProcessing { get; private set; }
    public DateTime? StartTime { get; private set; }

    


    public async void ProjectTextures()
    {
        IsProcessing = true;
        // HasErrorOccurred = false;
        var propertyComponent = gameObject.GetComponents<Component>()[0];

        string uniqueFileName = gameObject.name + ".fbx";
        string creationTime = gameObject.GetComponents<PropertyComponent>()[0].creationTime;
        Texture2D projection_image = gameObject.GetComponents<PropertyComponent>()[0].texture;
        // UnityEngine.Debug.Log(projection_image);
        // UnityEngine.Debug.Log(creationTime);
        // UnityEngine.Debug.Log(uniqueFileName);

        if (this.queue_position == 0)
        {        
            this.queue_position = Utility.FindMaxQueuePosition()+1;
            EditorUtility.SetDirty(this);

            // StartTime = DateTime.Now;
            // string nowString = DateTime.Now.ToString("HH-mm-ss");            
            
            await Utility.WaitUntilQueuePositionIsOne(gameObject);            

            if (queue_position == 1)
            {
                    // string pythonScriptPath = Path.Combine(Application.dataPath, "AtlasGeneratorBeta/Editor/client_3D.py");
                    string programPath = Path.Combine(Application.dataPath, "AtlasGeneratorBeta/Editor/client3D/client3D.exe");
                    string projectRootPath = Application.dataPath.Replace("/Assets", "");

                    // Use unique file names for each process
                    // string sourcePath = Path.Combine(projectRootPath, uniqueFileName); // Unique result file
                    string destinationPath = Path.Combine(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx",""), uniqueFileName);

                    // string original_img_path = Application.dataPath + "/AtlasGeneratorBeta/images/original-images/"+iconTexture.name+".png";
                    string original_img_path = Application.dataPath + "/AtlasGeneratorBeta/images/original-images/"+projection_image.name;
                    original_img_path = original_img_path+Utility.FindImagePathWithExtension(original_img_path);
                                                    
                    string img_path = Application.dataPath + "/AtlasGeneratorBeta/images/temp-images/"+projection_image.name + "-" + creationTime + Utility.FindImagePathWithExtension(original_img_path);
                    Utility.CopyImage(original_img_path, img_path);
                    Utility.OverwriteFirstPixelWithRandomColor(img_path);

                    string outputPath = destinationPath.Replace("/","\\");
                    img_path = img_path.Replace("/","\\");

                    if (!Directory.Exists(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","")))
                    {
                        Directory.CreateDirectory(Application.dataPath + "/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx",""));
                    }

                    UnityEngine.Debug.Log(outputPath);
                    UnityEngine.Debug.Log(img_path);
                    await Utility.ExecuteClient3DAsync(programPath, img_path, "generate-fbx", outputPath);
                                    
                    // Wait for the file without blocking Unity

                    while(!(await Utility.WaitForFileAsync(destinationPath)))
                    {
                        AssetDatabase.Refresh();
                        Thread.Sleep(1000);
                        // throw new Exception("File did not appear in time.");
                    }
                                       
                    // Validate the FBX file before moving or importing it
                    while (!Utility.IsFBXFileValid(destinationPath))
                    {
                        Thread.Sleep(1000);
                    }                

                    string importerPath = "Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","/")+ uniqueFileName;
                    // string importerPath = destinationPath;
                        
                    // Get the importer for the FBX file
                    ModelImporter modelImporter = AssetImporter.GetAtPath(importerPath) as ModelImporter;
                    if (modelImporter == null)
                    {
                        UnityEngine.Debug.LogError("I was not possible to get the ModelImporter del FBX: " + importerPath);
                        return;
                    }
                    // Get the directory containing the FBX file
                    string fbxDirectory = Path.GetDirectoryName(importerPath);

                    // If the directory does not exist then creates one
                    if (!Directory.Exists(fbxDirectory))
                    {
                        Directory.CreateDirectory(fbxDirectory);
                    }
                    // Extraxt the texture and save it on the same  directory of the FBX file
                    modelImporter.ExtractTextures(fbxDirectory);

                    // Apply ModelImporter
                    AssetDatabase.ImportAsset(importerPath);
                    // UnityEngine.Debug.Log($"Texturas extraídas a: {fbxDirectory}");    
                    
                    gameObject.name = projection_image.name;
                    // RemoveAllComponents(gameObject);

                    // GameObject fbxObject = AssetDatabase.LoadAssetAtPath<GameObject>(Path.Combine("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx",""), uniqueFileName));
                    GameObject fbxObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","/")+ uniqueFileName);
                    UnityEngine.Debug.Log("Assets/AtlasGeneratorBeta/GeneratedModels/"+uniqueFileName.Replace(".fbx","/")+ uniqueFileName);
                    // Verificar si se cargó correctamente
                    if (fbxObject != null)
                    {
                        GameObject newInstance = Instantiate(fbxObject);
                        newInstance.name = fbxObject.name;
                        newInstance.transform.position = gameObject.transform.position;
                        // newInstance.transform.localScale = new Vector3(15f,15f,15f);
                        // newInstance.transform.localScale *= billboardSize;
                        Utility.DecreaseQueuePosition();
                        DestroyImmediate(gameObject);                    
                        AssetDatabase.Refresh();                    
                        return;
                    }
                    
                    AssetDatabase.Refresh();

            }
        }    
        IsProcessing = false;
        // Notify the request queue that the request has been completed
    }
}

[CustomEditor(typeof(PropertyComponent))]
[CanEditMultipleObjects]
public class PropertyComponentEditor : Editor
{
    public override void OnInspectorGUI()
    {
    // Dibujar los elementos predeterminados del Inspector
        DrawDefaultInspector();

        // Obtener el componente seleccionado
        PropertyComponent propertyComponent = (PropertyComponent)target;
        // PropertyComponent propertyComponent = gameObj;
        // Verificar si readyToTex es true
        if (propertyComponent.readyToTex)
        {
            if (propertyComponent.IsProcessing)
            {
                // Disable the button and show "Processing" label
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("Project Texture");
                EditorGUI.EndDisabledGroup();
                // Display "Processing..." message
                EditorGUILayout.HelpBox("Processing... Please wait.", MessageType.Info);

                // Calculate and display the elapsed time
                if (propertyComponent.StartTime.HasValue)
                {
                    TimeSpan elapsedTime = DateTime.Now - propertyComponent.StartTime.Value;
                    EditorGUILayout.LabelField("Elapsed Time:", $"{elapsedTime.Minutes:D2}:{elapsedTime.Seconds:D2}");
                }
                Repaint();
            }
            // Mostrar el botón "Project Texture"
            else 
            {
            // Acción cuando se presiona el botón
                if(GUILayout.Button("Project Texture"))
                {
                    UnityEngine.Debug.Log($"{propertyComponent.name} - Project Texture button clicked.");
                    propertyComponent.ProjectTextures();
                }                    
            }
        }
    }
}


    


public class QueuePositionWindow : EditorWindow
{
    [MenuItem("Atlas Generator/Processes Queue")]
    public static void ShowWindow()
    {
        GetWindow<QueuePositionWindow>("Processes Queue");
    }

    private Vector2 scrollPosition;

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        // Get all GameObjects in the scene
        GameObject[] objects = FindObjectsOfType<GameObject>();

        // Create a list of objects with queue_position field
        List<(GameObject obj, int queuePosition)> objectsWithQueuePosition = new List<(GameObject obj, int queuePosition)>();
        foreach (GameObject obj in objects)
        {
            foreach (var component in obj.GetComponents<Component>())
            {
                // Try to get `queue_position` as a field
                var queuePositionField = component.GetType().GetField("queue_position", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (queuePositionField != null && queuePositionField.FieldType == typeof(int))
                {
                    int queuePosition = (int)queuePositionField.GetValue(component);
                    objectsWithQueuePosition.Add((obj, queuePosition));
                    break;
                }
            }
        }

        // Order objects by queue_position field
        objectsWithQueuePosition = objectsWithQueuePosition.OrderBy(item => item.queuePosition).ToList();

        // Display objects in the window
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Name", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Queue Position", EditorStyles.boldLabel);
        EditorGUILayout.EndHorizontal();

        foreach (var item in objectsWithQueuePosition)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(item.obj.name);
            EditorGUILayout.LabelField(item.queuePosition.ToString());
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }
}

public class PipelineSelectorWindow : EditorWindow
{
    private static readonly string jsonPath = "Assets/AtlasGeneratorBeta/pipelines/";
    private string[] jsonFiles;
    private int selectedIndex = 0;
    private const string selectedFileKey = "ActivePipelineFile";

    [MenuItem("Atlas Generator/Pipeline Selector")]
    public static void ShowWindow()
    {
        GetWindow<PipelineSelectorWindow>("Pipeline Selector");
    }

    private void OnEnable()
    {
        // Cargar los archivos .json en la carpeta
        jsonFiles = Directory.GetFiles(jsonPath, "*.json")
                             .Select(Path.GetFileNameWithoutExtension)
                             .ToArray();

        // Cargar el índice seleccionado previamente
        selectedIndex = EditorPrefs.GetInt(selectedFileKey, 0);
    }

    private void OnGUI()
    {
        if (jsonFiles.Length == 0)
        {
            EditorGUILayout.LabelField("No JSON files found in the specified path.");
            return;
        }

        EditorGUILayout.LabelField("Select a pipeline JSON file:");

        // Dropdown para seleccionar el archivo JSON
        selectedIndex = EditorGUILayout.Popup(selectedIndex, jsonFiles);

        // Botón para guardar la selección actual como activa
        if (GUILayout.Button("Set Active Pipeline"))
        {
            EditorPrefs.SetInt(selectedFileKey, selectedIndex);
            UnityEngine.Debug.Log($"Active pipeline set to: {jsonFiles[selectedIndex]}");
        }
    }

    public static string GetActivePipelineFilePath()
    {
        int index = EditorPrefs.GetInt(selectedFileKey, 0);
        string fileName = Path.GetFileNameWithoutExtension(Directory.GetFiles(jsonPath, "*.json")[index]);
        return Path.Combine(jsonPath, $"{fileName}.json");
    }
}
