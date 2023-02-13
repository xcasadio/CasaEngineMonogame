//-----------------------------------------------------------------------------
// Move.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using System.Xml;
using CasaEngineCommon.Design;
using CasaEngineCommon.Extension;

namespace CasaEngine.Input.InputSequence
{
    public class Move
    {
        public string Name;

        // The sequence of button presses required to activate this move.
        //public Buttons[] Sequence;
        //public int[] Sequence;
#if EDITOR
        public List<List<InputManager.KeyState>> Sequence = new();
#else
        public InputManager.KeyState[][] Sequence; 
#endif

        // Set this to true if the input used to activate this move may
        // be reused as a component of longer moves.
        public bool IsSubMove;

        public Move(string name)
        {
            Name = name;
        }

        public Move(XmlElement el, SaveOption option)
        {
            Load(el, option);
        }

        public void Load(XmlElement el, SaveOption option)
        {
            Name = el.Attributes["name"].Value;

            var nodes = el.SelectSingleNode("SequenceList").ChildNodes;

#if !EDITOR
            Sequence = new InputManager.KeyState[nodes.Count][];
#endif

            for (var i = 0; i < nodes.Count; i++)
            {
                var seqNodes = nodes[i].SelectNodes("Button");
#if EDITOR
                Sequence.Add(new List<InputManager.KeyState>());
#else
                Sequence[i] = new InputManager.KeyState[seqNodes.Count];
#endif

                for (var j = 0; j < seqNodes.Count; j++)
                {
#if EDITOR
                    var k = new InputManager.KeyState();
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

        public void Save(XmlElement el, SaveOption option)
        {
            el.OwnerDocument.AddAttribute(el, "name", Name);

            var seq = el.OwnerDocument.CreateElement("SequenceList");
            el.AppendChild(seq);

            foreach (var tab in Sequence)
            {
                var s = el.OwnerDocument.CreateElement("Sequence");
                seq.AppendChild(s);

                foreach (var k in tab)
                {
                    var but = el.OwnerDocument.CreateElement("Button");
                    but.OwnerDocument.AddAttribute(but, "key", k.Key.ToString());
                    but.OwnerDocument.AddAttribute(but, "state", Enum.GetName(typeof(ButtonState), k.State));
                    but.OwnerDocument.AddAttribute(but, "time", k.Time.ToString());
                    s.AppendChild(but);
                }
            }
        }

        public void Save(BinaryWriter bw, SaveOption option)
        {
            bw.Write(Sequence.Count);

            foreach (var tab in Sequence)
            {
                bw.Write(tab.Count);

                foreach (var k in tab)
                {
                    bw.Write(k.Key);
                    bw.Write((int)k.State);
                    bw.Write(k.Time);
                }
            }
        }

#endif

        public bool Match(int i, InputManager.KeyState[] buttons)
        {
            var x = 0;

            foreach (var k in Sequence[i])
            {
                foreach (var b in buttons)
                {
                    if (b.Match(k))
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
