//-----------------------------------------------------------------------------
// Move.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System;


using System.Xml;
using CasaEngine;
using CasaEngineCommon.Extension;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.IO;
using CasaEngineCommon.Design;


namespace CasaEngine.Input
{
    /// <summary>
    /// Describes a sequences of buttons which must be pressed to active the move.
    /// A real game might add a virtual PerformMove() method to this class.
    /// </summary>
    public class Move
    {
        public string Name;

        // The sequence of button presses required to activate this move.
        //public Buttons[] Sequence;
        //public int[] Sequence;
#if EDITOR
        public List<List<InputManager.KeyState>> Sequence = new List<List<InputManager.KeyState>>();
#else
        public InputManager.KeyState[][] Sequence; 
#endif

        // Set this to true if the input used to activate this move may
        // be reused as a component of longer moves.
        public bool IsSubMove;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="sequence"></param>
        public Move(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public Move(XmlElement el_, SaveOption option_)
        {
            Load(el_, option_);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="el_"></param>
        /// <param name="option_"></param>
        public void Load(XmlElement el_, SaveOption option_)
        {
            Name = el_.Attributes["name"].Value;

            XmlNodeList nodes = el_.SelectSingleNode("SequenceList").ChildNodes;

#if !EDITOR
            Sequence = new InputManager.KeyState[nodes.Count][];
#endif

            for (int i = 0; i < nodes.Count; i++)
            {
                XmlNodeList seqNodes = nodes[i].SelectNodes("Button");
#if EDITOR
                Sequence.Add(new List<InputManager.KeyState>());
#else
                Sequence[i] = new InputManager.KeyState[seqNodes.Count];
#endif

                for (int j = 0; j < seqNodes.Count; j++)
                {
#if EDITOR
                    InputManager.KeyState k = new InputManager.KeyState();
                    k.Key = int.Parse(seqNodes[j].Attributes["key"].Value);
                    k.State = (ButtonState)Enum.Parse(typeof(ButtonState), seqNodes[j].Attributes["state"].Value);
                    k.Time = float.Parse(seqNodes[j].Attributes["time"].Value);

                    Sequence[i].Add(k);
#else
                    Sequence[i][j].Key = int.Parse(seqNodes[j].Attributes["key"].Value);
                    Sequence[i][j].State = (ButtonState)Enum.Parse(typeof(ButtonState), seqNodes[j].Attributes["state"].Value);
                    Sequence[i][j].Time = float.Parse(seqNodes[j].Attributes["time"].Value);
#endif
                }
            }
        }

#if EDITOR

        /// <summary>
		/// 
		/// </summary>
		/// <param name="el_"></param>
		/// <param name="option_"></param>
		public void Save(XmlElement el_, SaveOption option_)
        {
            el_.OwnerDocument.AddAttribute(el_, "name", Name);

            XmlElement seq = el_.OwnerDocument.CreateElement("SequenceList");
            el_.AppendChild(seq);

            foreach (List<InputManager.KeyState> tab in Sequence)
            {
                XmlElement s = el_.OwnerDocument.CreateElement("Sequence");
                seq.AppendChild(s);

                foreach (InputManager.KeyState k in tab)
                {
                    XmlElement but = el_.OwnerDocument.CreateElement("Button");
                    but.OwnerDocument.AddAttribute(but, "key", k.Key.ToString());
                    but.OwnerDocument.AddAttribute(but, "state", Enum.GetName(typeof(ButtonState), k.State));
                    but.OwnerDocument.AddAttribute(but, "time", k.Time.ToString());
                    s.AppendChild(but);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bw_"></param>
        /// <param name="option_"></param>
        public void Save(BinaryWriter bw_, SaveOption option_)
        {
            bw_.Write(Sequence.Count);

            foreach (List<InputManager.KeyState> tab in Sequence)
            {
                bw_.Write(tab.Count);

                foreach (InputManager.KeyState k in tab)
                {
                    bw_.Write(k.Key);
                    bw_.Write((int)k.State);
                    bw_.Write(k.Time);
                }
            }
        }

#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <param name="buttons_"></param>
        /// <returns></returns>
        public bool Match(int i, InputManager.KeyState[] buttons_)
        {
            int x = 0;

            foreach (InputManager.KeyState k in Sequence[i])
            {
                foreach (InputManager.KeyState b in buttons_)
                {
                    if (b.Match(k) == true)
                    {
                        x++;
                        break;
                    }
                }
            }

#if EDITOR
            return x == Sequence[i].Count;
#else
            return x == Sequence[i].Length;
#endif
        }
    }
}
