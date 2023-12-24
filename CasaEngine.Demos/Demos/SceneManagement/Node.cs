using System;
using System.Collections.Generic;
using CasaEngine.Core.Helpers;
using Microsoft.Xna.Framework;

namespace Veldrid.SceneGraph
{
    public abstract class Node : Object, INode
    {
        public Guid Id { get; private set; }
        public uint NodeMask { get; set; } = 0xffffffff;
        public string NameString { get; set; } = string.Empty;
        public int NumParents => _parents.Count;
        public bool CullingActive { get; set; } = true;
        public int NumChildrenWithCullingDisabled { get; set; } = 0;
        public bool IsCullingActive => NumChildrenWithCullingDisabled == 0 && CullingActive && GetBound().Valid();
        public IPipelineState PipelineState { get; set; } = null;
        public bool HasPipelineState => null != PipelineState;

        public int GetNumChildrenRequiringEventTraversal()
        {
            throw new NotImplementedException();
        }

        private int _numChildrentRequiringUpdateTraversal = 0;
        public int GetNumChildrenRequiringUpdateTraversal()
        {
            return _numChildrentRequiringUpdateTraversal;
        }

        public void SetNumChildrenRequiringEventTraversal(int i)
        {
            throw new NotImplementedException();
        }

        public void SetNumChildrenRequiringUpdateTraversal(int i)
        {
            _numChildrentRequiringUpdateTraversal = i;
        }

        // Protected/Private fields
        private List<IGroup> _parents;
        protected bool _boundingSphereComputed = false;
        protected BoundingSphere _boundingSphere = BoundingSphereExtension.Create();


        private BoundingSphere _initialBound = BoundingSphereExtension.Create();
        public BoundingSphere InitialBound
        {
            get { return _initialBound; }
            set
            {
                _initialBound = value;
                DirtyBound();
            }
        }

        public event Func<Node, BoundingSphere> ComputeBoundCallback;

        private Action<INodeVisitor, INode> _updateCallback;

        public virtual void SetUpdateCallback(Action<INodeVisitor, INode> callback)
        {
            _updateCallback = callback;

            //            var collectParentsVisitor = new CollectParentPaths();
            //            Accept(collectParentsVisitor);
            //            
            //            foreach (var parentNodePath in collectParentsVisitor.NodePaths)
            //            {
            //                foreach(var parentNode in parentNodePath)
            //                {
            //                    parentNode.SetNumChildrenRequiringUpdateTraversal(parentNode.GetNumChildrenRequiringUpdateTraversal()+1);
            //                }
            //            }
        }
        public virtual Action<INodeVisitor, INode> GetUpdateCallback()
        {
            return _updateCallback;
        }

        protected Node()
        {
            Id = Guid.NewGuid();
            _updateCallback = null;

            _parents = new List<IGroup>();
        }

        public void AddParent(IGroup parent)
        {
            _parents.Add(parent);
        }

        public void RemoveParent(IGroup parent)
        {
            _parents.RemoveAll(x => x.Id == parent.Id);
        }

        public void DirtyBound()
        {
            if (!_boundingSphereComputed) return;

            _boundingSphereComputed = false;

            foreach (var parent in _parents)
            {
                parent.DirtyBound();
            }
        }

        public BoundingSphere GetBound()
        {
            if (_boundingSphereComputed) return _boundingSphere;

            _boundingSphere = _initialBound;

            _boundingSphere.ExpandBy(ComputeBoundCallback?.Invoke(this) ?? ComputeBound());

            _boundingSphereComputed = true;

            return _boundingSphere;
        }

        /// <summary>
        /// Compute the bounding sphere of this geometry
        /// </summary>
        /// <returns></returns>
        public virtual BoundingSphere ComputeBound()
        {
            return BoundingSphereExtension.Create();
        }

        public virtual void Accept(INodeVisitor nv)
        {
            if (nv.ValidNodeMask(this))
            {
                nv.PushOntoNodePath(this);
                nv.Apply(this);
                nv.PopFromNodePath(this);
            };
        }

        public virtual void Ascend(INodeVisitor nv)
        {
            foreach (var parent in _parents)
            {
                parent.Accept(nv);
            }
        }

        // Traverse downward - call children's accept method with Node Visitor
        public virtual void Traverse(INodeVisitor nv)
        {
            // Do nothing by default
        }
    }
}

