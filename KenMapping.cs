using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Specialized;
// Terminology: ShareFileClient:      represents a FILE in the cloud.      (Ken also calls it CloudFile)
//              ShareDirectoryClient: represents a DIRECTORY in the cloud. (Ken also calls it CloudDirectory)

// Design: These are simply methods to help generate cloud files/directories that correspond to local filepaths/dirpaths (and vice versa).
// Concept: We must know the cloud top level directory and the local top level directory.
//          The relative path below the top level directory will be the same when mapping cloud-to-local or vice versa.
// Examples: Assume the cloud top level directory is simply the root directory of an Azure FileShare (with share name = kentutorialfileshare).
//           Assume the 
//          Example 1: cloudRootPath corresponds to C:\
//          Example 2: cloudRootPath/CloudDir1 corresponds to C:\MyProject\LocalDir1\LocalDir2
//          Example 3: cloudRootPath/CloudDir1 corresponds to \\MyServer2\LocalDir1
//          Knowing the top level directories, we can map files/directories from the cloud to local file system and vice versa.
namespace KenAzureFileShareLibrary
{
    public class KenMapping
    {
        public ShareDirectoryClient CloudTopLevelDir { get; set; }
        public string LocalTopLevelDir { get; set; }
        public KenMapping(ShareDirectoryClient cloudTopLevelDir, string localTopLevelDir)
        {
            CloudTopLevelDir = cloudTopLevelDir;
            LocalTopLevelDir = localTopLevelDir;
        }
        /// <summary>
        /// If cloudDirectoryPath=null then the CloudTopLevelDir will be the root directory of the fileshare (ShareClient).
        /// </summary>
        /// <param name="shareClient"></param>
        /// <param name="cloudDirectoryPath"></param>
        /// <param name="localTopLevelDir"></param>
        public KenMapping(ShareClient shareClient, string cloudDirectoryPath, string localTopLevelDir)
        {
            CloudTopLevelDir = KenAzureFileUtilities.CreateShareDirectoryClient(shareClient, cloudDirectoryPath);
            LocalTopLevelDir = localTopLevelDir;
        }
        /// <summary>
        /// Map a cloud file (ShareFileClient) to a local filePath (DOS or UNC).
        /// A localTopLevelDir must provide the UNC server name or DOS drive; that path may include additional subdirectories.
        /// </summary>
        /// <param name="localTopLevelDir"></param>
        /// <param name="cloudTopLevelDir"></param>
        /// <param name="shareFileClient"></param>
        /// <returns></returns>
        public string MapCloudFileToLocalFilePath(ShareFileClient shareFileClient)
        {
            // Ex: localTopLevelDir = @"\\Server2\" for a UNC path
            // Ex: localTopLevelDir = @"C:\" for a traditional DOS path
            // Ex: localTopLevelDir = @"C:\somedir1\somedir2\" for a traditional DOS path
            string relativePath = shareFileClient.Path.Substring(CloudTopLevelDir.Path.Length);
            // trim leading "/" so Path.Combine works correctly
            relativePath = relativePath.TrimStart('/');
            string localFilePath = Path.Combine(LocalTopLevelDir, relativePath);
            // not really necessary; .NET will accept either separator (even intermixed!)
            localFilePath = localFilePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return localFilePath;
        }
        /// <summary>
        /// Map a cloud directory (ShareDirectoryClient) to a local directory path (DOS or UNC).
        /// A localTopLevelDir must provide the UNC server name or DOS drive; that path may include additional subdirectories.
        /// </summary>
        /// <param name="localTopLevelDir"></param>
        /// <param name="cloudTopLevelDir"></param>
        /// <param name="shareDirectoryClient"></param>
        /// <returns></returns>
        public string MapCloudDirectoryToLocalDirectoryPath(ShareDirectoryClient shareDirectoryClient)
        {
            // Ex: localTopLevelDir = @"\\Server2\" for a UNC path
            // Ex: localTopLevelDir = @"C:\" for a traditional DOS path
            // Ex: localTopLevelDir = @"C:\somedir1\somedir2\" for a traditional DOS path
            string relativePath = shareDirectoryClient.Path.Substring(CloudTopLevelDir.Path.Length);
            // trim leading "/" so Path.Combine works correctly
            relativePath = relativePath.TrimStart('/');
            string localDirectoryPath = Path.Combine(LocalTopLevelDir, relativePath);
            // not really necessary; .NET will accept either separator (even intermixed!)
            localDirectoryPath = localDirectoryPath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return localDirectoryPath;
        }
        /// <summary>
        /// Map a localFilePath to a cloud file (ShareFileClient).
        /// The localFilePath must be a traditional DOS path (absolute or relative) or a UNC path
        /// </summary>
        /// <param name="localTopLevelDir"></param>
        /// <param name="cloudTopLevelDir"></param>
        /// <param name="localFilePath"></param>
        /// <returns></returns>
        public ShareFileClient MapLocalFilePathToCloudFile(string localFilePath)
        {
            string relativePath = localFilePath.Substring(LocalTopLevelDir.Length);
            relativePath = relativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            ShareClient shareClient = CloudTopLevelDir.GetParentShareClient();
            string wholePath = CloudTopLevelDir.Path + relativePath;
            ShareFileClient shareFileClient = KenAzureFileUtilities.CreateShareFileClient(shareClient, wholePath);
            return shareFileClient;
        }
        /// <summary>
        /// Map a localDirectoryPath (path of a directory) to a cloud directory (ShareDirectoryClient).
        /// The localDirectoryPath must be a traditional DOS path (absolute or relative) or a UNC path
        /// </summary>
        /// <param name="localTopLevelDir"></param>
        /// <param name="cloudTopLevelDir"></param>
        /// <param name="localFilePath"></param>
        /// <returns></returns>
        public ShareDirectoryClient MapLocalDirectoryPathToCloudDirectory(string localDirectoryPath)
        {
            string relativePath = localDirectoryPath.Substring(LocalTopLevelDir.Length);
            relativePath = relativePath.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            ShareClient shareClient = CloudTopLevelDir.GetParentShareClient();
            string wholePath = CloudTopLevelDir.Path + relativePath;
            ShareDirectoryClient shareDirectoryClient = KenAzureFileUtilities.CreateShareDirectoryClient(shareClient, wholePath);
            return shareDirectoryClient;
        }
    }
}
