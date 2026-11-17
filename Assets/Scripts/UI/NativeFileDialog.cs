using System.IO;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

namespace GoG
{
    public static class NativeFileDialog
    {
        public static string OpenJson(string title, string initialDirectory)
        {
            initialDirectory = ExistingDirectory(initialDirectory);

#if UNITY_EDITOR
            return UnityEditor.EditorUtility.OpenFilePanelWithFilters(
                title,
                initialDirectory,
                new[] { "JSON files", "json,parsed.json", "All files", "*" });
#elif UNITY_STANDALONE_WIN
            return WindowsDialog(title, initialDirectory, null, false);
#else
            Debug.LogWarning("File dialog is only available in the Unity Editor and Windows builds.");
            return null;
#endif
        }

        public static string SaveJson(string title, string initialDirectory, string defaultFileName)
        {
            initialDirectory = ExistingDirectory(initialDirectory);
            defaultFileName = EnsureJsonName(defaultFileName);

#if UNITY_EDITOR
            return UnityEditor.EditorUtility.SaveFilePanel(
                title, initialDirectory, defaultFileName, "json");
#elif UNITY_STANDALONE_WIN
            return WindowsDialog(title, initialDirectory, defaultFileName, true);
#else
            Debug.LogWarning("File dialog is only available in the Unity Editor and Windows builds.");
            return null;
#endif
        }

        static string ExistingDirectory(string initialDirectory)
        {
            if (string.IsNullOrEmpty(initialDirectory) || !Directory.Exists(initialDirectory))
                return Application.dataPath;
            return initialDirectory;
        }

        public static string EnsureJsonName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "map.parsed.json";
            fileName = Path.GetFileName(fileName.Trim());
            if (string.IsNullOrEmpty(fileName))
                return "map.parsed.json";
            if (!fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                fileName += ".parsed.json";
            return fileName;
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        sealed class OpenFileName
        {
            public int structSize;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter;
            public string customFilter;
            public int maxCustFilter;
            public int filterIndex;
            public string file;
            public int maxFile;
            public string fileTitle;
            public int maxFileTitle;
            public string initialDir;
            public string title;
            public int flags;
            public short fileOffset;
            public short fileExtension;
            public string defExt;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt;
            public int flagsEx;
        }

        [DllImport("Comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("Comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetSaveFileName([In, Out] OpenFileName ofn);

        const int OfnExplorer = 0x00080000;
        const int OfnFileMustExist = 0x00001000;
        const int OfnPathMustExist = 0x00000800;
        const int OfnHideReadOnly = 0x00000004;
        const int OfnNoChangeDir = 0x00000008;
        const int OfnOverwritePrompt = 0x00000002;

        static string WindowsDialog(string title, string initialDirectory, string defaultFileName, bool save)
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(typeof(OpenFileName));
            ofn.filter = "JSON files (*.json)\0*.json;*.parsed.json\0All files (*.*)\0*.*\0";
            string file = string.IsNullOrEmpty(defaultFileName) ? "" : defaultFileName;
            ofn.file = file.PadRight(1024, '\0');
            ofn.maxFile = 1024;
            ofn.fileTitle = new string('\0', 256);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.initialDir = initialDirectory;
            ofn.title = title;
            ofn.defExt = "json";
            ofn.flags = OfnExplorer | OfnPathMustExist | OfnHideReadOnly | OfnNoChangeDir;
            if (save)
                ofn.flags |= OfnOverwritePrompt;
            else
                ofn.flags |= OfnFileMustExist;

            bool ok = save ? GetSaveFileName(ofn) : GetOpenFileName(ofn);
            if (!ok)
                return null;
            return ofn.file.Split('\0')[0];
        }
#endif
    }
}
