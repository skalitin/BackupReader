
namespace BackupReader
{
    enum ENodeType : int 
    {
        Root = 0,
        Set = 1,
        Volume = 2,
        Folder = 3,
        File = 4,
    }

    /// <summary>
    /// Catalog nodes represents the blocks in the backup file.
    /// </summary>
    class CCatalogNode
    {
        private string mName;
        private ENodeType mType;
        private long mOffset;
        private CCatalogNode mParent;
        private System.Collections.Generic.List<CCatalogNode> mNodes;
        private CDescriptorBlock mDescriptorBlock;

        public CDescriptorBlock DescriptorBlock
        {
            get { return mDescriptorBlock; }
        }

        public string Name
        {
            get { return mName; }
        }

        public ENodeType Type
        {
            get { return mType; }
        }

        public long Offset
        {
            get { return mOffset; }
        }

        protected CCatalogNode Parent
        {
            get { return mParent; }
        }

        public System.Collections.Generic.List<CCatalogNode> Children
        {
            get { return mNodes; }
        }

        public CCatalogNode()
        {
            mDescriptorBlock = null;
            mName = "";
            mType = ENodeType.Root;
            mOffset = 0;
            mParent = null;
            mNodes = new System.Collections.Generic.List<CCatalogNode>();
        }

        public CCatalogNode(CDescriptorBlock descriptorBlock, string nName, ENodeType nType)
        {
            mDescriptorBlock = descriptorBlock;
            mName = nName;
            mType = nType;
            mOffset = descriptorBlock.StartPosition;
            mParent = null;
            mNodes = new System.Collections.Generic.List<CCatalogNode>();
        }

        public CCatalogNode AddSet(CStartOfDataSetDescriptorBlock descriptorBlock)
        {
            var name = "Set: " + descriptorBlock.DataSetNumber + " - " + descriptorBlock.DataSetName;
            var cnode = new CCatalogNode(descriptorBlock, name, ENodeType.Set);
            cnode.mParent = this;
            mNodes.Add(cnode);
            return cnode;
        }

        public CCatalogNode AddVolume(CVolumeDescriptorBlock descriptorBlock)
        {
            var cnode = new CCatalogNode(descriptorBlock, descriptorBlock.DeviceName, ENodeType.Volume);
            cnode.mParent = this;
            mNodes.Add(cnode);
            return cnode;
        }

        public CCatalogNode AddFolder(CDirectoryDescriptorBlock descriptorBlock, string nName)
        {
            var cnode = new CCatalogNode(descriptorBlock, nName, ENodeType.Folder);
            cnode.mParent = this;
            mNodes.Add(cnode);
            return cnode;
        }

        public CCatalogNode AddFile(CFileDescriptorBlock descriptorBlock, string nName)
        {
            var cnode = new CCatalogNode(descriptorBlock, nName, ENodeType.File);
            cnode.mParent = this;
            mNodes.Add(cnode);
            return cnode;
        }

        public bool ExtractTo(CBackupReader backupFile, string targetPath)
        {
            // Ensure that the target path has a trailing '\'
            if (targetPath[targetPath.Length - 1] != '\\')
                targetPath += '\\';

            if ((mType == ENodeType.Root) || (mType == ENodeType.Set))
            {
                throw new System.Exception("Tape and set nodes can not be extracted. Only volume, folder or file nodes can be extracted.");
            }
            else if (mType == ENodeType.Volume)
            {
                System.IO.DirectoryInfo dirinfo = System.IO.Directory.CreateDirectory(targetPath);
                foreach (CCatalogNode node in mNodes)
                    node.ExtractTo(backupFile, targetPath);
            }
            else if (mType == ENodeType.Folder)
            {
                System.IO.DirectoryInfo dirinfo = System.IO.Directory.CreateDirectory(targetPath + mName);
                foreach (CCatalogNode node in mNodes)
                    node.ExtractTo(backupFile, dirinfo.FullName);
            }
            else if (mType == ENodeType.File)
            {
                // Create the target directory if it does not exist
                System.IO.Directory.CreateDirectory(targetPath);
                backupFile.Stream.BaseStream.Seek(mOffset, System.IO.SeekOrigin.Begin);
                System.IO.FileStream file = new System.IO.FileStream(targetPath + mName, System.IO.FileMode.Create);
                CFileDescriptorBlock fil = (CFileDescriptorBlock)backupFile.Stream.ReadDBLK();
                foreach (CDataStream data in fil.Streams)
                {
                    if (data.Header.StreamID == "STAN")
                    {
                        file.Write(data.Data, 0, data.Data.Length);
                    }
                }
                file.Close();
            }

            return true;
        }

        /// <summary>
        /// Saves the catalog to the disk.
        /// </summary>
        public static void SaveCatalog(string Filename, CCatalogNode Node, string BackupFilename)
        {
            // Open the file
            System.IO.BinaryWriter file = new System.IO.BinaryWriter(new System.IO.FileStream(Filename, System.IO.FileMode.Create, System.IO.FileAccess.Write));

            // Write full path to backup file
            file.Write(BackupFilename);

            // Write nodes
            Node.SaveNode(file);

            // Close the file
            file.Close();
        }

        /// <summary>
        /// Reads the name of the backup file used to create the catalog.
        /// </summary>
        public static string ReadBackupFilename(string Filename)
        {
            // Open the file
            System.IO.BinaryReader file = new System.IO.BinaryReader(new System.IO.FileStream(Filename, System.IO.FileMode.Open, System.IO.FileAccess.Read));

            // Read backup file name
            string bkfname = file.ReadString();

            // Close the file
            file.Close();

            return bkfname;
        }

        /// <summary>
        /// Reads the catalog from the disk.
        /// </summary>
        public static CCatalogNode ReadCatalog(string Filename)
        {
            // Create the root node
            CCatalogNode Node = new CCatalogNode();

            // Open the file
            System.IO.BinaryReader file = new System.IO.BinaryReader(new System.IO.FileStream(Filename, System.IO.FileMode.Open, System.IO.FileAccess.Read));

            // Read backup file name
            file.ReadString();

            // Read nodes
            Node.ReadNode(file);

            // Close the file
            file.Close();

            return Node;
        }

        private void SaveNode(System.IO.BinaryWriter file)
        {
            // Write node info
            file.Write((int)mType);
            file.Write(mName);
            file.Write(mOffset);
            file.Write(mNodes.Count);

            // Recursively write child nodes
            foreach (CCatalogNode node in mNodes)
                node.SaveNode(file);
        }

        private void ReadNode(System.IO.BinaryReader file)
        {
            // Read node info
            mType = (ENodeType)file.ReadInt32();
            mName = file.ReadString();
            mOffset = file.ReadInt64();
            int count = file.ReadInt32();

            // Recursively read child nodes
            for (int i = 0; i < count; i++)
            {
                CCatalogNode node = new CCatalogNode();
                mNodes.Add(node);
                node.ReadNode(file);
            }
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Type: {1}, Offset: {2}, Block: {3}", Name, Type, Offset, DescriptorBlock);
        }
    }

}
