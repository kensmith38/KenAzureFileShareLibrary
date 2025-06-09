using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
// Reference: https://learn.microsoft.com/en-us/dotnet/api/azure.storage.files.shares?view=azure-dotnet

namespace KenAzureFileShareLibrary
{
    // Design: A ShareClient must be obtained before using any utility method since it is a parm for all methods.
    //      The utility methods simplify the coding to perform common tasks involving Azure storage file shares.
    // Terminology: ShareFileClient:      represents a FILE in the cloud.      (Ken also calls it CloudFile)
    //              ShareDirectoryClient: represents a DIRECTORY in the cloud. (Ken also calls it CloudDirectory)
    // Tricky! When you "Create" a ShareFileClient or ShareFileDirectory that cloud object is not physically created!
    //         A physical file/directory gets created when you act on the object (ex: Upload will create the physical file).
    public static class KenAzureFileUtilities
    {
        /// <summary>
        /// Initialize a new ShareClient and physically create it's directory if it does not exist (based on parm).
        /// This needs to be called only one time per ShareClient!
        /// Your azureStorageConnectionString can be found on the Azure portal at 
        ///       your_azure_storageaccount/Data storage/Security+networking/Access keys.
        /// Your fileShareName (after you create one) can be found on the Azure portal at 
        ///       your_azure_storageaccount/Data storage/File shares.
        /// Note that this library allows you to create a new FileShare or use an existing one!
        /// </summary>
        public static ShareClient GetShareClient(string azureStorageConnectionString, string fileShareName, bool createIfNotExist)
        {
            ShareClient shareClient = new ShareClient(azureStorageConnectionString, fileShareName);
            if (createIfNotExist)
            {
                shareClient.CreateIfNotExists();
            }
            return shareClient;
        }
        /// <summary>
        /// Create a ShareFileClient from a cloudFilePath; the path must include the file name.
        /// The cloudFilePath must not include the root directory name of the fileShare.
        /// EX: fileShare is the KenTutorialFileShare (with root directory name = kentutorialfileshare).
        ///     cloudFilePath = "subfolder1/subfolder2/myfile.ext"  (subfolders are optional)
        /// </summary>
        /// <param name="shareClient"></param>
        /// <param name="cloudFilePath"></param>
        /// <returns></returns>
        public static ShareFileClient CreateShareFileClient(ShareClient shareClient, string cloudFilePath)
        {
            ShareFileClient shareFileClient = null;
            ShareDirectoryClient rootDirectory = shareClient.GetRootDirectoryClient();
            int indexOfPathSeparator = cloudFilePath.LastIndexOf("/");
            string filename = indexOfPathSeparator < 0 ? cloudFilePath : cloudFilePath.Substring(indexOfPathSeparator + 1);
            string directoryPath = indexOfPathSeparator < 0 ? null : cloudFilePath.Substring(0, indexOfPathSeparator);
            ShareDirectoryClient shareDirectoryClient = directoryPath == null ? 
                rootDirectory :
                rootDirectory.GetSubdirectoryClient(directoryPath);
            shareFileClient = shareDirectoryClient.GetFileClient(filename);
            return shareFileClient;
        }
        /// <summary>
        /// Create a ShareDirectoryClient from a cloudDirectoryPath; the path must NOT include the file name.
        /// The cloudDirectoryPath must not include the root directory name of the fileShare.
        /// If cloudDirectoryPath is null, this returns the root directory of the FileShare.
        /// EX: fileShare is the KenTutorialFileShare (with root directory name = kentutorialfileshare).
        ///     cloudDirectoryPath = "subfolder1/subfolder2"  (subfolders are optional)
        /// </summary>
        /// <param name="shareClient"></param>
        /// <param name="cloudDirectoryPath"></param>
        /// <returns></returns>
        public static ShareDirectoryClient CreateShareDirectoryClient(ShareClient shareClient, string cloudDirectoryPath)
        {
            ShareDirectoryClient shareDirectoryClient = null;
            ShareDirectoryClient rootDirectory = shareClient.GetRootDirectoryClient();
            shareDirectoryClient = cloudDirectoryPath == null ?
                rootDirectory :
                rootDirectory.GetSubdirectoryClient(cloudDirectoryPath);
            return shareDirectoryClient;
        }
        /// <summary>
        /// Physically create cloud directories specified in the cloudDirectoryPath.
        /// The cloudDirectoryPath is relative to the root directory of the shareClient (file share).
        /// It is OK if any of the subdirectories already exist.
        /// </summary>
        /// <param name="shareClient"></param>
        /// <param name="cloudDirectoryPath"></param>
        public static void CreatePhysicalCloudDirectories(ShareClient shareClient, string cloudDirectoryPath)
        {
            ShareDirectoryClient shareDirectoryClient = CreateShareDirectoryClient(shareClient, cloudDirectoryPath);
            InternalUtilities.CreateParentDirectories(shareDirectoryClient);
        }
        /// <summary>
        /// Physically create cloud subdirectories specified in the cloudSubDirectoryPath.
        /// The cloudSubDirectoryPath is relative to the specified topLevelDirectory.
        /// It is OK if any of the subdirectories already exist.
        /// </summary>
        /// <param name="shareClient"></param>
        /// <param name="cloudDirectoryPath"></param>
        public static void CreatePhysicalCloudSubDirectories(ShareClient shareClient, ShareDirectoryClient topLevelDirectory ,string cloudSubDirectoryPath)
        {
            if (!topLevelDirectory.Exists()) { throw new Exception("Error! The top level directory does not exist."); }
            string cloudEntireDirectoryPath = cloudSubDirectoryPath.StartsWith("/") ?
                topLevelDirectory.Path + cloudSubDirectoryPath :
                topLevelDirectory.Path + "/" + cloudSubDirectoryPath;
            ShareDirectoryClient shareDirectoryClient = CreateShareDirectoryClient(shareClient, cloudEntireDirectoryPath);
            InternalUtilities.CreateParentDirectories(shareDirectoryClient);
        }
        /// <summary>
        /// Get a list of all cloud files in the specified cloud directory.  A parameter specifies if all subdirectories should be traversed.
        /// </summary>
        /// <param name="shareDirectoryClient"></param>
        /// <returns></returns>
        public static List<ShareFileClient> GetListAllCloudFiles(ShareDirectoryClient shareDirectoryClient, bool traverseAllSubdirectories,
            List<ShareFileClient> listShareFileClients = null)
        {
            if (listShareFileClients == null) { listShareFileClients = new List<ShareFileClient>();}
            // Note: The "name" and "IsDirectory" properties of a ShareFileItem can be used to obtain the ShareDirectoryClient or ShareFileClient for the item! 
            List<ShareFileItem> listShareFileItems = shareDirectoryClient.GetFilesAndDirectories().ToList();
            foreach (ShareFileItem shareItem in listShareFileItems)
            {
                if (shareItem.IsDirectory)
                {
                    if (traverseAllSubdirectories)
                    {
                        ShareDirectoryClient subdir = shareDirectoryClient.GetSubdirectoryClient(shareItem.Name);
                        GetListAllCloudFiles(subdir, traverseAllSubdirectories, listShareFileClients);
                    }
                }
                else
                {
                    ShareFileClient cloudFile = shareDirectoryClient.GetFileClient(shareItem.Name);
                    listShareFileClients.Add(cloudFile);
                }
            }
            return listShareFileClients;
        }
        /// <summary>
        /// Get a list of all cloud subdirectories in the specified cloud directory.  A parameter specifies if all subdirectories should be traversed.
        /// </summary>
        /// <param name="shareDirectoryClient"></param>
        /// <returns></returns>
        public static List<ShareDirectoryClient> GetListAllCloudDirectories(ShareDirectoryClient shareDirectoryClient, bool traverseAllSubdirectories,
            List<ShareDirectoryClient> listShareDirectoryClients = null)
        {
            if (listShareDirectoryClients == null) { listShareDirectoryClients = new List<ShareDirectoryClient>(); }
            // Note: The "name" and "IsDirectory" properties of a ShareFileItem can be used to obtain the ShareDirectoryClient or ShareFileClient for the item! 
            List<ShareFileItem> listShareFileItems = shareDirectoryClient.GetFilesAndDirectories().ToList();
            foreach (ShareFileItem shareItem in listShareFileItems)
            {
                if (shareItem.IsDirectory)
                {
                    ShareDirectoryClient subdir = shareDirectoryClient.GetSubdirectoryClient(shareItem.Name);
                    listShareDirectoryClients.Add(subdir);
                    if (traverseAllSubdirectories)
                    {
                        GetListAllCloudDirectories(subdir, traverseAllSubdirectories, listShareDirectoryClients);
                    }
                }
            }
            return listShareDirectoryClients;
        }
        /// <summary>
        /// Upload contents of a local directory to a specific cloud directory (ShareDirectoryClient).
        /// A parm indicates if the method should recursively traverseAllSubdirectories.
        /// A parm indicates maxsizeMB allowed (default is 10).
        /// </summary>
        /// <param name="localPath"></param>
        /// <param name="cloudDirectory"></param>
        /// <param name="traverseAllSubdirectories"></param>
        public static void UploadLocalDirectoryToCloud(string localPath, ShareDirectoryClient cloudDirectory, bool traverseAllSubdirectories, long maxsizeMB=10)
        {
            DirectoryInfo localDirInfo = new DirectoryInfo(localPath);
            if (!localDirInfo.Exists) { throw new Exception($"Error! Local directory does not exist: {localPath}"); }
            long totalBytes = InternalUtilities.GetLocalDirectorySize(localPath, traverseAllSubdirectories);
            long bytesPerMB = 1024 * 1024;
            long totalMB = totalBytes / bytesPerMB;
            // safety check
            if (totalMB > maxsizeMB)
            {
                throw new Exception($"The directory size is {totalMB}MB; this exceeds {maxsizeMB}MB.  The maxsizeMB parm can be set to a higher value.");
            }
            InternalUtilities.UploadDirectoryContentToCloud(localPath, cloudDirectory, traverseAllSubdirectories);
        }
        /// <summary>
        /// Download a cloud directory to a local directory.
        /// A parm indicates if the method should recursively traverseAllSubdirectories.
        /// A parm indicates maxsizeMB allowed (default is 10).
        /// </summary>
        /// <param name="shareClient"></param>
        /// <param name="cloudDirectoryPath"></param>
        /// <param name="localDirectoryPath"></param>
        public static void DownloadCloudDirectoryToLocalDirectory(ShareDirectoryClient cloudDirectory, string localPath, bool traverseAllSubdirectories, long maxsizeMB = 10)
        {
            if (!cloudDirectory.Exists()) { throw new Exception($"Error! Cloud directory does not exist: {cloudDirectory.Path}"); }
            long totalBytes = InternalUtilities.GetCloudDirectorySize(cloudDirectory, traverseAllSubdirectories);
            long bytesPerMB = 1024 * 1024;
            long totalMB = totalBytes / bytesPerMB;
            // safety check
            if (totalMB > maxsizeMB)
            {
                throw new Exception($"The directory size is {totalMB}MB; this exceeds {maxsizeMB}MB.  The maxsizeMB parm can be set to a higher value.");
            }
            InternalUtilities.DownloadCloudDirectoryContentToLocalDirectory(cloudDirectory, localPath,traverseAllSubdirectories);
        }

        /// <summary>
        /// Download a cloud file to a localFilePath. 
        /// Parameters specify if the parent directories for the local file can be created or if an existing local file can be overwritten.
        /// </summary>
        /// <param name="shareFileClient"></param>
        /// <param name="localFilePath"></param>
        public static void DownloadFile(ShareFileClient shareFileClient, string localFilePath, bool createLocalParentDirectories, bool allowOverwrite)
        {
            if (!allowOverwrite && File.Exists(localFilePath))
            {
                throw new Exception($"Cannot overwrite existing file: {localFilePath}");
            }
            // createLocalParentDirectories if they don't exist
            string localDirectoryPath = Path.GetDirectoryName(localFilePath);
            Directory.CreateDirectory(localDirectoryPath);
            ShareFileDownloadInfo download = shareFileClient.Download();
            using (FileStream stream = File.Create(localFilePath))
            {
                download.Content.CopyTo(stream);
            }
        }
        /// <summary>
        /// Download a cloud file to a byte array. 
        /// </summary>
        /// <param name="shareFileClient"></param>
        /// <param name="localFilePath"></param>
        public static byte [] DownloadFile(ShareFileClient shareFileClient)
        {
            ShareFileDownloadInfo download = shareFileClient.Download();
            // maximum content size is slightly less than 2 GB
            int size = (int)download.ContentLength;
            using (MemoryStream memstream = new MemoryStream(size))
            {
                download.Content.CopyTo(memstream);
                return memstream.ToArray();
            }
        }
        /// <summary>
        /// Copies a local file to the cloud.
        /// Parms determine if an existing ShareFileClient can be overwritten and if parent directories should be created (if they don't already exist).
        /// </summary>
        /// <param name="shareFileClient"></param>
        /// <param name="localFilePath"></param>
        /// <param name="allowOverwrite"></param>
        public static void UploadFile(ShareFileClient shareFileClient, string localFilePath, bool allowOverwrite, bool createCloudDirectories = true)
        {
            using (FileStream fileStream = File.OpenRead(localFilePath))
            {
                InternalUtilities.UploadFile(shareFileClient, fileStream, allowOverwrite, createCloudDirectories);
            }
        }
        /// <summary>
        /// Creates a file in the cloud and writes the provided bytes content to that file.
        /// Parms determine if an existing ShareFileClient can be overwritten and if parent directories should be created (if they don't already exist).
        /// </summary>
        /// <param name="shareFileClient"></param>
        /// <param name="bytes"></param>
        /// <param name="allowOverwrite"></param>
        public static void UploadFile(ShareFileClient shareFileClient, byte[] bytes, bool allowOverwrite, bool createCloudDirectories = true)
        {
            using (MemoryStream memstream = new MemoryStream(bytes))
            {
                InternalUtilities.UploadFile(shareFileClient, memstream, allowOverwrite, createCloudDirectories);
            }
        }
        /// <summary>
        /// Creates a file in the cloud and writes the provided memory stream content to that file.
        /// Parms determine if an existing ShareFileClient can be overwritten and if parent directories should be created (if they don't already exist).
        /// </summary>
        /// <param name="shareFileClient"></param>
        /// <param name="memstream"></param>
        /// <param name="allowOverwrite"></param>
        public static void UploadFile(ShareFileClient shareFileClient, MemoryStream memstream, bool allowOverwrite, bool createCloudDirectories = true)
        {
            // Useful information: Note that a MemoryStream can be created by a byte array, an Image, and many others!
            using (memstream)
            {
                InternalUtilities.UploadFile(shareFileClient, memstream, allowOverwrite, createCloudDirectories);
            }
        }
        /// <summary>
        /// Get the metadata dictionary for an existing object which must be a ShareFileClient or a ShareDirectoryClient.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Dictionary<string, string> GetMetadataDictionary(Object obj)
        {
            Dictionary<string, string> metadataDict = null;
            InternalUtilities.ThrowExceptionIfObjectNotExistingCloudFileOrCloudDirectory(obj);
            if (obj.GetType() == typeof(ShareFileClient))
            {
                ShareFileClient shareFileClient = (ShareFileClient)obj;
                ShareFileProperties shareFileProperties = shareFileClient.GetProperties();
                metadataDict = (Dictionary<string, string>)shareFileProperties.Metadata;
            }
            if (obj.GetType() == typeof(ShareDirectoryClient))
            {
                ShareDirectoryClient shareDirectoryClient = (ShareDirectoryClient)obj;
                ShareDirectoryProperties shareDirectoryProperties = shareDirectoryClient.GetProperties();
                metadataDict = (Dictionary<string, string>)shareDirectoryProperties.Metadata;
            }
            return metadataDict;
        }
        /// <summary>
        /// Adds an item (key/value pair) to the existing metadata dictionary for an existing object which must be a ShareFileClient or a ShareDirectoryClient.  
        /// Parameter, replaceIfKeyAlreadyExists, specifies if item should be replaced if the key already exists.
        /// An exception is thrown if the key already exists and replaceIfKeyAlreadyExists=false.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="replaceIfKeyAlreadyExists"></param>
        public static void AddMetadataItem(object obj, string key, string value, bool replaceIfKeyAlreadyExists)
        {
            InternalUtilities.ThrowExceptionIfObjectNotExistingCloudFileOrCloudDirectory(obj);
            Dictionary<string, string> metadataDict = GetMetadataDictionary(obj);
            bool keyAlreadyExists = metadataDict.ContainsKey(key);
            if (keyAlreadyExists && replaceIfKeyAlreadyExists)
            {
                metadataDict.Remove(key);
            }
            metadataDict.Add(key, value);
            if (obj.GetType() == typeof(ShareFileClient))
            {
                ShareFileClient shareFileClient = (ShareFileClient)obj;
                shareFileClient.SetMetadata(metadataDict);
            }
            if (obj.GetType() == typeof(ShareDirectoryClient))
            {
                ShareDirectoryClient shareDirectoryClient = (ShareDirectoryClient)obj;
                shareDirectoryClient.SetMetadata(metadataDict);
            }
        }
        /// <summary>
        /// Remove an item (key/value pair) from the existing metadata dictionary for an existing object which must be a ShareFileClient or a ShareDirectoryClient.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="key"></param>
        public static void RemoveMetadataItem(object obj, string key)
        {
            InternalUtilities.ThrowExceptionIfObjectNotExistingCloudFileOrCloudDirectory(obj);
            Dictionary<string, string> metadataDict = GetMetadataDictionary(obj);
            metadataDict.Remove(key);
            if (obj.GetType() == typeof(ShareFileClient))
            {
                ShareFileClient shareFileClient = (ShareFileClient)obj;
                shareFileClient.SetMetadata(metadataDict);
            }
            if (obj.GetType() == typeof(ShareDirectoryClient))
            {
                ShareDirectoryClient shareDirectoryClient = (ShareDirectoryClient)obj;
                shareDirectoryClient.SetMetadata(metadataDict);
            }
        }
        /// <summary>
        /// Clears (purges) the existing metadata dictionary for existing object which must be a ShareFileClient or a ShareDirectoryClient.
        /// </summary>
        /// <param name="shareFileClient"></param>
        public static void ClearMetadata(object obj)
        {
            InternalUtilities.ThrowExceptionIfObjectNotExistingCloudFileOrCloudDirectory(obj);
            if (obj.GetType() == typeof(ShareFileClient))
            {
                ShareFileClient shareFileClient = (ShareFileClient)obj;
                shareFileClient.SetMetadata(new Dictionary<string, string>());
            }
            if (obj.GetType() == typeof(ShareDirectoryClient))
            {
                ShareDirectoryClient shareDirectoryClient = (ShareDirectoryClient)obj;
                shareDirectoryClient.SetMetadata(new Dictionary<string, string>());
            }
        }
    }
}
