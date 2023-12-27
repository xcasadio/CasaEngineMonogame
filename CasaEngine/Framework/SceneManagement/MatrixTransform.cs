using Microsoft.Xna.Framework;

namespace CasaEngine.Framework.SceneManagement;

public class MatrixTransform : Transform, IMatrixTransform
{
    private Matrix _matrix = Matrix.Identity;

    public Matrix Matrix
    {
        get => _matrix;
        private set
        {
            _matrix = value;
            _inverseDirty = true;
            DirtyBound();
        }
    }

    private bool _inverseDirty = true;
    private Matrix _inverse = Matrix.Identity;
    public Matrix Inverse
    {
        get
        {
            if (_inverseDirty)
            {
                Matrix.Invert(ref _matrix, out _inverse);
                _inverseDirty = false;
            }

            return _inverse;
        }
    }

    protected MatrixTransform(Matrix matrix)
    {
        Matrix = matrix;
    }

    public static IMatrixTransform Create(Matrix matrix)
    {
        return new MatrixTransform(matrix);
    }

    public void PreMultiply(Matrix mat)
    {
        _matrix = mat * _matrix;
        _inverseDirty = true;
        DirtyBound();
    }

    public void PostMultiply(Matrix mat)
    {
        _matrix *= mat;
        _inverseDirty = true;
        DirtyBound();
    }

    public override bool ComputeLocalToWorldMatrix(ref Matrix matrix, NodeVisitor visitor)
    {
        if (ReferenceFrame == ReferenceFrameType.Relative)
        {
            matrix *= _matrix;
        }
        else // absolute
        {
            matrix = _matrix;
        }
        return true;
    }

    public override bool ComputeWorldToLocalMatrix(ref Matrix matrix, NodeVisitor visitor)
    {
        if (ReferenceFrame == ReferenceFrameType.Relative)
        {
            matrix = Inverse * matrix;
        }
        else // absolute
        {
            matrix = Inverse;
        }
        return true;
    }
}