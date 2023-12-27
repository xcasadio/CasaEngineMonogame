﻿using Microsoft.Xna.Framework.Graphics;

namespace CasaEngine.Framework.SceneManagement;

public class ShapeDrawable<T> : Geometry<T>, IShapeDrawable<T> where T : struct, ISettablePrimitiveElement, IVertexType
{
    /*private IShape _shape;
        private Vector3[] _colors;
        private ITessellationHints _hints;

        public static IShapeDrawable<T> Create(IShape shape, ITessellationHints hints)
        {
            return new ShapeDrawable<T>(shape, hints);
        }

        public static IShapeDrawable<T> Create(IShape shape, ITessellationHints hints, Vector3[] colors)
        {
            return new ShapeDrawable<T>(shape, hints, colors);
        }

        internal ShapeDrawable(IShape shape, ITessellationHints hints)
        {
            if (hints.ColorsType == ColorsType.ColorOverall)
            {
                SetColors(new[] { Vector3.One });
            }
            else if (hints.ColorsType == ColorsType.ColorPerFace)
            {
                var colors = new List<Vector3>();
                if (shape is IBox)
                {
                    for (var f = 0; f < 6; ++f)
                    {
                        colors.Append(Vector3.One);
                    }
                    SetColors(colors.ToArray());
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else if (hints.ColorsType == ColorsType.ColorPerVertex)
            {
                var colors = new List<Vector3>();
                if (shape is IBox)
                {
                    for (var f = 0; f < 24; ++f)
                    {
                        colors.Append(Vector3.One);
                    }
                    SetColors(colors.ToArray());
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            SetShape(shape);
            SetTessellationHints(hints);
            Build();
        }

        internal ShapeDrawable(IShape shape, ITessellationHints hints, Vector3[] colors)
        {
            SetColors(colors);
            SetShape(shape);
            SetTessellationHints(hints);
            Build();
        }

        private void SetShape(IShape shape)
        {
            _shape = shape;
        }

        private void SetColors(Vector3[] colors)
        {
            _colors = colors;
        }

        private void SetTessellationHints(ITessellationHints hints)
        {
            _hints = hints;
        }

        private void Build()
        {
            var shapeGeometryVisitor = new BuildShapeGeometryVisitor<T>(this, _hints, _colors);
            _shape.Accept(shapeGeometryVisitor);
        }*/
}