using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.SourceControl;

namespace Editor.SourceControl
{
	public partial class SourceControlConnectionForm : System.Windows.Forms.Form
	{
        /// <summary>
        /// 
        /// </summary>
		public SourceControlConnectionForm()
		{
			InitializeComponent();

			textBoxServer.Text = SourceControlManager.Instance.Server;
            textBoxUser.Text = SourceControlManager.Instance.User;
            textBoxPassword.Text = SourceControlManager.Instance.Password;
            textBoxWorkspace.Text = SourceControlManager.Instance.Workspace;
		}

		/// <summary>
		/// Gets
		/// </summary>
		public string Server
		{
			get { return textBoxServer.Text; }
		}

		/// <summary>
		/// Gets
		/// </summary>
		public string User
		{
			get { return textBoxUser.Text; }
		}

		/// <summary>
		/// Gets
		/// </summary>
		public string Password
		{
			get { return textBoxPassword.Text; }
		}

		/// <summary>
		/// Gets
		/// </summary>
		public string Workspace
		{
			get { return textBoxWorkspace.Text; }
		}
	}
}
