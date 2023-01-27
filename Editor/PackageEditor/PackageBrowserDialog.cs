using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Game;
using CasaEngine.Project;

namespace Editor.PackageEditor
{
    public partial class PackageBrowserDialog : Form
    {
        public PackageBrowserDialog()
        {
            InitializeComponent();

            treeViewPackage.Nodes.Add("");
            treeViewPackage.SelectedNode = treeViewPackage.TopNode;

            /*foreach (Package p in GameInfo.Instance.PackageManager.Packages)
            {
                TreeNode node = new TreeNode(p.Name);
                treeViewPackage.Nodes.Add(node);

                foreach (string dir in p.SubDirectories)
                {

                    node.Nodes.Add(dir);
                }
            }*/
        }

        /// <summary>
        /// Gets the path of the package or subdirectory selected.
        /// If any return string.Empty
        /// </summary>
        public string SelectedPackagePath
        {
            get
            {
                return treeViewPackage.SelectedNode != null ? treeViewPackage.SelectedNode.FullPath : string.Empty;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            TreeNode node = treeViewPackage.SelectedNode;
            //Package p;
            string pckName = "New folder";

            if ( node == null)
            {
                node = treeViewPackage.TopNode;
            }
            
            /*p = GameInfo.Instance.PackageManager.GetPackage(node.Name);

            if (p == null)
            {
                p = GameInfo.Instance.PackageManager.CreatePackage(pckName);
            }*/

            TreeNode n = new TreeNode(pckName);
            node.Nodes.Add(n);
            node.Expand();

            treeViewPackage.SelectedNode = n;
        }
    }
}
