//-----------------------------------------------------------------------------
// EditCurveState.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;

namespace Editor.Tools.CurveEditor
{
    /// <summary>
    /// This class contians curve states which are name, PreLoop, and PostLoop.
    /// </summary>
    public class EditCurveState : IEquatable<EditCurveState>
    {

        /// <summary>
        /// Gets/Sets Name
        /// </summary>
        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        /// <summary>
        /// Gets/Sets PreLoop
        /// </summary>
        public CurveLoopType PreLoop
        {
            get { return preLoop; }
            set { preLoop = value; }
        }

        /// <summary>
        /// Gets/Sets PostLoop
        /// </summary>
        public CurveLoopType PostLoop
        {
            get { return postLoop; }
            set { postLoop = value; }
        }


        public override int GetHashCode()
        {
            return string.IsNullOrEmpty(Name) ? 0 : Name.GetHashCode() +
                PreLoop.GetHashCode() + +PostLoop.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            EditCurveState other = obj as EditCurveState;
            bool isSame = false;
            if (other != null)
            {
                isSame = Equals(other);
            }

            return isSame;
        }

        public bool Equals(EditCurveState other)
        {
            if (other == null)
            {
                return false;
            }

            return (Name == other.Name &&
                    PreLoop == other.PreLoop &&
                    PostLoop == other.PostLoop);
        }


        /// <summary>
        /// Create close of this state.
        /// </summary>
        /// <returns></returns>
        public EditCurveState Clone()
        {
            EditCurveState newState = new EditCurveState();
            newState.Name = Name;
            newState.PreLoop = PreLoop;
            newState.PostLoop = PostLoop;
            return newState;
        }


        public static bool operator ==(EditCurveState value1, EditCurveState value2)
        {
            return Equals(value1, value2);
        }

        public static bool operator !=(EditCurveState value1, EditCurveState value2)
        {
            return !Equals(value1, value2);
        }



        private string name;
        private CurveLoopType preLoop;
        private CurveLoopType postLoop;


    }
}
