using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Editor.WinForm
{
	/// <summary>
	/// Notify when controls changed state
	/// </summary>
	public class ObserverControlData
	{
		#region Fields

		Control[] m_Controls = null;
		Dictionary<Control, object> m_ControlDatas = new Dictionary<Control, object>();
		Dictionary<Control, bool> m_ControlStates = new Dictionary<Control, bool>();
		//int nbChanges = 0;
		bool m_Start = false;

		public event EventHandler StateChanged;

		#endregion

		#region Properties

		/// <summary>
		/// Gets
		/// </summary>
		public bool IsChanged
		{
			get 
			{
				foreach (KeyValuePair<Control, bool> pair in m_ControlStates)
				{
					if (pair.Value == false)
					{
						return true;
					}
				}

				return false;
			}
		}

		#endregion

		#region Constructors

		/// <summary>
		/// 
		/// </summary>
		/// <param name="controls_"></param>
		public ObserverControlData(Control[] controls_)
		{
			if (controls_  == null)
			{
				throw new ArgumentException("ObserverControlData() : Control[] is null");
			}
			
			m_Controls = controls_;

			Initialize();
		}

		#endregion

		#region Methods

		/// <summary>
		/// 
		/// </summary>
		public void Reset()
		{
			Initialize();
		}

		/// <summary>
		/// 
		/// </summary>
		public void Start()
		{
			m_Start = true;
		}

		/// <summary>
		/// 
		/// </summary>
		private void Initialize()
		{
			m_ControlDatas.Clear();
			m_ControlStates.Clear();

			foreach (Control c in m_Controls)
			{
				if (c is ComboBox)
				{
					((ComboBox)c).SelectedIndexChanged += new EventHandler(DataChanged);
					m_ControlDatas.Add(c, ((ComboBox)c).SelectedIndex);
				}
				else if (c is TextBox)
				{
					((TextBox)c).TextChanged += new EventHandler(DataChanged);
					m_ControlDatas.Add(c, ((TextBox)c).Text);
				}
				else if (c is RadioButton)
				{
					((RadioButton)c).CheckedChanged += new EventHandler(DataChanged);
					m_ControlDatas.Add(c, ((RadioButton)c).Checked);
				}
				else if (c is CheckBox)
				{
					((CheckBox)c).CheckedChanged += new EventHandler(DataChanged);
					m_ControlDatas.Add(c, ((CheckBox)c).Checked);
				} // important before ListBox
				else if (c is CheckedListBox)
				{
					((CheckedListBox)c).ItemCheck += new ItemCheckEventHandler(ItemCheck);
					m_ControlDatas.Add(c, ((CheckedListBox)c).CheckedIndices);
				}
				else if (c is ListBox)
				{
					((ListBox)c).SelectedIndexChanged += new EventHandler(DataChanged);
					m_ControlDatas.Add(c, ((ListBox)c).SelectedIndex);
				}
				else if (c is ListView)
				{
					((ListView)c).SelectedIndexChanged += new EventHandler(DataChanged);
					m_ControlDatas.Add(c, ((ListView)c).SelectedIndices);
				}
				
				else if (c is NumericUpDown)
				{
					((NumericUpDown)c).ValueChanged += new EventHandler(DataChanged);
					m_ControlDatas.Add(c, ((NumericUpDown)c).Value);
				}
				else
				{
					throw new ArgumentException("ObserverControlData.Initialize() : Control type not supported " + c.GetType().Name);
				}
			}

			foreach (Control c in m_Controls)
			{
				m_ControlStates.Add(c, true);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void ItemCheck(object sender, ItemCheckEventArgs e)
		{
			if (m_Start == false)
			{
				return;
			}

			Control c = ((Control)sender);
			object data = m_ControlDatas[c];

			if (c is CheckedListBox)
			{
				m_ControlStates[c] = ((CheckedListBox)c).CheckedIndices.Equals(data);
			}
			else
			{
                throw new ArgumentException("ObserverControlData.ItemCheck() : Control type not supported " + c.GetType().Name);
			}

			if (StateChanged != null)
			{
				StateChanged(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void DataChanged(object sender, EventArgs e)
		{
			if (m_Start == false)
			{
				return;
			}

			Control c = ((Control)sender);
			object data = m_ControlDatas[c];

			if (c is ComboBox)
			{
				m_ControlStates[c] = ((ComboBox)c).SelectedIndex.Equals(data);
			}
			else if (c is TextBox)
			{
				m_ControlStates[c] = ((TextBox)c).Text.Equals(data);
			}
			else if (c is RadioButton)
			{
				m_ControlStates[c] = ((RadioButton)c).Checked.Equals(data);
			}
			else if (c is CheckBox)
			{
				m_ControlStates[c] = ((CheckBox)c).Checked.Equals(data);
			}
			else if (c is ListBox /*&& (c is CheckedListBox) == false*/)
			{
				m_ControlStates[c] = ((ListBox)c).SelectedIndex.Equals(data);
			}
			else if (c is ListView)
			{
				m_ControlStates[c] = ((ListView)c).SelectedIndices.Equals(data);
			}
			else if (c is NumericUpDown)
			{
				m_ControlStates[c] = ((NumericUpDown)c).Value.Equals(data);
			}
			else
			{
                //throw new ArgumentException("ObserverControlData.DataChanged() : Control type not supported " + c.GetType().Name);
			}

			if (StateChanged != null)
			{
				StateChanged(this, EventArgs.Empty);
			}
		}

		#endregion
	}
}
