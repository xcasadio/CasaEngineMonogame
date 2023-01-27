#if UNITTEST

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Microsoft.Xna.Framework.Input;

namespace CasaEngine.Input
{
    /// <summary>
    /// 
    /// </summary>
    public partial class InputManager
    {
        /// <summary>
        /// Only one button held
        /// </summary>
        [Test]
        public void SimpleTest()
        {
            KeyState k;
            Move move;

            m_Buffer.Clear();

            List<KeyState[]> KeyStateArrayList = new List<KeyState[]>();
            List<KeyState> KeyStateList = new List<KeyState>();

            //1
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            float g = 0f;
            foreach (KeyState[] a in KeyStateArrayList)
            {
                Update(a, g);
                g += MergeInputTime * 2.0f; //avoid merge
            }

            move = new Move("test");
            move.IsSubMove = false;
            move.Name = "test";
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;

#if EDITOR
            List<KeyState> l = new List<KeyState>();
            l.Add(k);
            move.Sequence.Add(l);
#else
            move.Sequence = new KeyState[][] { new KeyState[] { k } };
#endif

            Assert.IsTrue(Matches(move));
        }

        /// <summary>
        /// 2 buttons pressed
        /// </summary>
        [Test]
        public void ComposedTest()
        {
            KeyState[][] keys;
            KeyState k;
            Move move;

            m_Buffer.Clear();

            List<KeyState[]> KeyStateArrayList = new List<KeyState[]>();
            List<KeyState> KeyStateList = new List<KeyState>();

            //1
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            //2
            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            float g = 0f;
            foreach (KeyState[] a in KeyStateArrayList)
            {
                Update(a, g);
                g += MergeInputTime * 2.0f; //merge
            }

            move = new Move("test");
            move.IsSubMove = false;
            keys = new KeyState[2][];
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;
            keys[0] = new KeyState[] { k };
            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;
            keys[1] = new KeyState[] { k };

#if EDITOR
            foreach (KeyState[] a in keys)
            {
                move.Sequence.Add(new List<KeyState>(a));
            }
#else
            move.Sequence = keys;
#endif

            Assert.IsTrue(Matches(move));
        }

        /// <summary>
        /// 
        /// </summary>
        [Test]
        public void ComplexTest()
        {
            KeyState k;

            m_Buffer.Clear();

            List<KeyState[]> KeyStateArrayList = new List<KeyState[]>();
            List<KeyState> KeyStateList = new List<KeyState>();

            //1
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 3;
            k.State = ButtonState.Released;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            //2
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Released;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Released;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 3;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            //3
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Released;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 3;
            k.State = ButtonState.Released;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            //4
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Released;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 3;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            //5
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            k = new KeyState();
            k.Key = 3;
            k.State = ButtonState.Released;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            float g = 0f;
            foreach (KeyState[] a in KeyStateArrayList)
            {
                Update(a, g);
                g += MergeInputTime * 2.0f; //avoid merge
            }

            Assert.AreEqual(m_Buffer.Count, 4);

            Assert.AreEqual(m_Buffer[0].KeysState.Count, 2);
            Assert.AreEqual(m_Buffer[1].KeysState.Count, 1);
            Assert.AreEqual(m_Buffer[2].KeysState.Count, 1);
            Assert.AreEqual(m_Buffer[3].KeysState.Count, 2);

            //1
            Assert.AreEqual(m_Buffer[0].KeysState[0].Key, 1);
            Assert.AreEqual(m_Buffer[0].KeysState[0].State, ButtonState.Pressed);
            Assert.AreEqual(m_Buffer[0].KeysState[0].Time, 0.016f);
            Assert.AreEqual(m_Buffer[0].KeysState[1].Key, 2);
            Assert.AreEqual(m_Buffer[0].KeysState[1].State, ButtonState.Pressed);
            Assert.AreEqual(m_Buffer[0].KeysState[1].Time, 0.016f);

            //2
            Assert.AreEqual(m_Buffer[1].KeysState[0].Key, 3);
            Assert.AreEqual(m_Buffer[1].KeysState[0].State, ButtonState.Pressed);
            Assert.AreEqual(m_Buffer[1].KeysState[0].Time, 0.016f);

            //3
            Assert.AreEqual(m_Buffer[2].KeysState[0].Key, 3);
            Assert.AreEqual(m_Buffer[2].KeysState[0].State, ButtonState.Pressed);
            Assert.AreEqual(m_Buffer[2].KeysState[0].Time, 0.016f);

            //4
            Assert.AreEqual(m_Buffer[3].KeysState[0].Key, 1);
            Assert.AreEqual(m_Buffer[3].KeysState[0].State, ButtonState.Pressed);
            Assert.AreEqual(m_Buffer[3].KeysState[0].Time, 0.048f);
            Assert.AreEqual(m_Buffer[3].KeysState[1].Key, 2);
            Assert.AreEqual(m_Buffer[3].KeysState[1].State, ButtonState.Pressed);
            Assert.AreEqual(m_Buffer[3].KeysState[1].Time, 0.016f);
        }

        [Test]
        public void MergedTest()
        {
            m_Buffer.Clear();

            KeyState[][] keys;
            KeyState k;
            Move move;

            List<KeyState[]> KeyStateArrayList = new List<KeyState[]>();
            List<KeyState> KeyStateList = new List<KeyState>();

            //1
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            //2
            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            float g = 0f;
            foreach (KeyState[] a in KeyStateArrayList)
            {
                Update(a, g);
                g += MergeInputTime / 2.0f; //merge
            }

            move = new Move("test");
            move.IsSubMove = false;
            keys = new KeyState[1][];
            keys[0] = new KeyState[2];
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;
            keys[0][0] = k;
            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;
            keys[0][1] = k;

#if EDITOR
            
#else
            move.Sequence = keys;
#endif

            Assert.IsTrue(Matches(move));
        }

        [Test]
        public void SubMoveTest()
        {
            KeyState[][] keys;
            KeyState k;
            Move move;

            m_Buffer.Clear();

            List<KeyState[]> KeyStateArrayList = new List<KeyState[]>();
            List<KeyState> KeyStateList = new List<KeyState>();

            //1
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            //2
            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.016f;
            KeyStateList.Add(k);

            KeyStateArrayList.Add(KeyStateList.ToArray());
            KeyStateList.Clear();

            Update(KeyStateArrayList[0], 0.0f);

            move = new Move("test");
            move.IsSubMove = true;
            keys = new KeyState[1][];
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;
            keys[0] = new KeyState[] { k };

#if EDITOR
            foreach (KeyState[] a in keys)
            {
                move.Sequence.Add(new List<KeyState>(a));
            }
#else
            move.Sequence = keys;
#endif

            Assert.IsTrue(Matches(move));

            Update(KeyStateArrayList[1], MergeInputTime * 2.0f);

            move = new Move("test");
            move.IsSubMove = false;
            keys = new KeyState[2][];
            k = new KeyState();
            k.Key = 1;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;
            keys[0] = new KeyState[] { k };
            k = new KeyState();
            k.Key = 2;
            k.State = ButtonState.Pressed;
            k.Time = 0.0f;
            keys[1] = new KeyState[] { k };

#if EDITOR
            foreach (KeyState[] a in keys)
            {
                move.Sequence.Add(new List<KeyState>(a));
            }
#else
            move.Sequence = keys;
#endif

            Assert.IsTrue(Matches(move));
        }
    }
}

#endif //UNITTEST
