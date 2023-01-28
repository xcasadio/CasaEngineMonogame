using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using CasaEngineCommon.Logger;
using System.Windows.Forms;
using System.Windows;
using CasaEngineCommon.Extension;

namespace Editor.Sprite2DEditor.SpriteSheetPacker
{
    /// <summary>
    /// 
    /// </summary>
    public class SpriteSheetTaskManager
    {

        static private int m_Version = 1;
        private List<SpriteSheetTask> m_Tasks = new List<SpriteSheetTask>();



        /// <summary>
        /// Gets
        /// </summary>
        public IEnumerable<SpriteSheetTask> Tasks
        {
            get { return m_Tasks; }
        }





        /// <summary>
        /// 
        /// </summary>
        /// <param name="task_"></param>
        public void AddTask(SpriteSheetTask task_)
        {
            if (task_ == null)
            {
                throw new ArgumentNullException("SpriteSheetTaskManager.AddTask() : SpriteSheetTask is null");
            }

            m_Tasks.Add(task_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indice_"></param>
        /// <returns></returns>
        public SpriteSheetTask GetTask(int indice_)
        {
            return m_Tasks[indice_];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="indice_"></param>
        /// <returns></returns>
        public void SetTask(int indice_, SpriteSheetTask t_)
        {
            m_Tasks[indice_] = t_;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index_"></param>
        public void RemoveTask(int index_)
        {
            m_Tasks.RemoveAt(index_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public SpriteSheetTaskManager Copy()
        {
            SpriteSheetTaskManager res = new SpriteSheetTaskManager();

            foreach (SpriteSheetTask t in m_Tasks)
            {
                res.m_Tasks.Add(t.Copy());
            }

            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        public void Load(string fileName_)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(fileName_);

                XmlElement rootNode = (XmlElement)xmlDoc.SelectSingleNode("SpriteSheetTask");
                int version = int.Parse(rootNode.Attributes["version"].Value);

                XmlElement nodeList = (XmlElement)rootNode.SelectSingleNode("TaskList");

                foreach (XmlNode node in nodeList.ChildNodes)
                {
                    SpriteSheetTask s = new SpriteSheetTask();
                    s.Load((XmlElement)node);
                    AddTask(s);
                }
            }
            catch (Exception ex_)
            {
                LogManager.Instance.WriteException(ex_);
                MessageBox.Show(ex_.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName_"></param>
        public void Save(string fileName_)
        {
            try
            {
                XmlDocument xmlDoc = new XmlDocument();

                XmlElement rootNode = xmlDoc.CreateElement("SpriteSheetTask");
                xmlDoc.AppendChild(rootNode);
                xmlDoc.AddAttribute(rootNode, "version", m_Version.ToString());

                XmlElement nodeList = xmlDoc.CreateElement("TaskList");
                rootNode.AppendChild(nodeList);                

                foreach (SpriteSheetTask t in m_Tasks)
                {
                    XmlElement el = xmlDoc.CreateElement("Task");
                    t.Save(el);
                    nodeList.AppendChild(el);
                }

                xmlDoc.Save(fileName_);
            }
            catch (Exception ex_)
            {
                LogManager.Instance.WriteException(ex_);
                MessageBox.Show(ex_.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="other_"></param>
        /// <returns>true if identical</returns>
        public bool Compare(SpriteSheetTaskManager other_)
        {
            if (m_Tasks.Count != other_.m_Tasks.Count)
            {
                return false;
            }

            for (int i = 0; i < m_Tasks.Count; i++)
            {
                if (m_Tasks[i].Compare(other_.m_Tasks[i]) == false)
                {
                    return false;
                }
            }

            return true;
        }

    }
}
