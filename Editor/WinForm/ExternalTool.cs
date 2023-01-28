
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using CasaEngine.Editor.Tools;

namespace Editor.WinForm
{
	/// <summary>
	/// 
	/// </summary>
	/*public abstract class ExternalTool
        : IExternalTool
	{

        static public SaveProjectDelegate SaveProject = null;

        public delegate void SaveProjectDelegate(object sender, EventArgs e);

        private EditorForm m_Form = null;



        /// <summary>
        /// Gets
        /// </summary>
        public EditorForm Form
        {
            get { return Form; }
            protected set 
            { 
                m_Form = value;

                if (SaveProject == null)
                {
                    throw new InvalidOperationException("ExternalTool.Form : the delegate SaveProject is not setted");
                }

                m_Form.SaveProject = (SaveProjectDelegate)SaveProject.Clone();
            }
        }





		/// <summary>
		/// 
		/// </summary>
		protected virtual void CreateForm()
		{
			m_Form.FormClosed += new System.Windows.Forms.FormClosedEventHandler(FormClosed);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
		{
			Close();
		}


		/// <summary>
		/// 
		/// </summary>
		public void Run(Form parent)
		{
			if (m_Form == null)
			{
				CreateForm();
			}
			else if (m_Form.IsDisposed == true)
			{
				CreateForm();
			}
			else // already opened
			{
				m_Form.Focus();
				return;
			}

			//why those conditions?
			//CreateForm can fail or the form can be close when opening
			if (m_Form != null
                || m_Form.IsDisposed == false)
			{
			    m_Form.Show();
			    m_Form.Focus();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		public void Close()
		{
			if (m_Form != null)
			{
				if (m_Form.IsDisposed == false)
				{
					m_Form.Dispose();
				}

				m_Form = null;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public abstract string GetMenuToolName();


	}*/
}
