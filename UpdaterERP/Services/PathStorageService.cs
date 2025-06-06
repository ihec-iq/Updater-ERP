﻿using System;
using System.IO;

namespace UpdaterMsarERP.Services
{
    public class PathStorageService
    {
        private readonly string pathsFilePath;

        public PathStorageService(string pathsFilePath)
        {
            this.pathsFilePath = pathsFilePath;
        }

        public string[] LoadPaths()
        {
            // Check if the file exists
            if (File.Exists(pathsFilePath))
            {
                try
                {
                    // Read all lines from the file
                    return File.ReadAllLines(pathsFilePath);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error loading paths: {ex.Message}");
                }
            }

            return Array.Empty<string>(); // Return an empty array if the file doesn't exist
        }

        public void SavePaths(string frontPath, string backPath,string phpIniPath)
        {
            try
            {
                // Save the paths to the file
                File.WriteAllLines(pathsFilePath, new[] { frontPath, backPath , phpIniPath });
            }
            catch (Exception ex)
            {
                throw new Exception($"Error saving paths: {ex.Message}");
            }
        }
    }
}