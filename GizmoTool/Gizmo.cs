
#define USE_QUATERNION
//#define USE_ROTATIONMATRIX

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

// -------------------------------------------------------------
// -- XNA 3D Gizmo (Component)
// -------------------------------------------------------------
// -- open-source gizmo component for any 3D level editor.
// -- contains any feature you may be looking for in a transformation gizmo.
// -- 
// -- for additional information and instructions visit codeplex.
// --
// -- codeplex url: http://xnagizmo.codeplex.com/
// --
// -----------------Please Do Not Remove ----------------------
// -- Work by Tom Looman, licensed under Ms-PL
// -- My Blog: http://coreenginedev.blogspot.com
// -- My Portfolio: http://tomlooman.com
// -- You may find additional XNA resources and information on these sites.
// ------------------------------------------------------------


namespace XNAGizmo
{
    public class Gizmo
    {
        /// <summary>
        /// only active if atleast one entity is selected.
        /// </summary>
        private bool _isActive = true;

        /// <summary>
        /// Enabled if gizmo should be able to select objects and axis.
        /// </summary>
        public bool Enabled { get; set; }

        public readonly bool SelectionBoxesIsVisible = true;

        private readonly GraphicsDevice _graphics;
        private readonly BasicEffect _lineEffect;
        private readonly BasicEffect _meshEffect;

        private Matrix _view = Matrix.Identity;
        private Matrix _projection = Matrix.Identity;
        private Vector3 _cameraPosition;

        // -- Screen Scale -- //
        private Matrix _screenScaleMatrix;
        private float _screenScale;

        // -- Position - Rotation -- //
        private Vector3 _position = Vector3.Zero;
        private Matrix _rotationMatrix = Matrix.Identity;

        private Vector3 _localForward = Vector3.Forward;
        private Vector3 _localUp = Vector3.Up;
        private Vector3 _localRight;

        // -- Matrices -- //
        private Matrix _objectOrientedWorld;
        private Matrix _axisAlignedWorld;
        private Matrix[] _modelLocalSpace;

        // used for all drawing, assigned by local- or world-space matrices
        private Matrix _gizmoWorld = Matrix.Identity;

        // the matrix used to apply to your whole scene, usually matrix.identity (default scale, origin on 0,0,0 etc.)
        public Matrix SceneWorld;

        // -- Lines (Vertices) -- //
        private VertexPositionColor[] _translationLineVertices;
        private const float LINE_LENGTH = 3f;
        private const float LINE_OFFSET = 1f;

        // -- Quads -- //
        private Quad[] _quads;
        private readonly BasicEffect _quadEffect;

        // -- Colors -- //
        private Color[] _axisColors;
        private Color _highlightColor;

        // -- UI Text -- //
        private string[] _axisText;
        private readonly Vector3 _axisTextOffset = new(0, 0.5f, 0);

        // -- Modes & Selections -- //
        private GizmoMode _activeMode = GizmoMode.Translate;
        private TransformSpace _activeSpace = TransformSpace.Local;

        public GizmoAxis ActiveAxis = GizmoAxis.None;

        public GizmoMode ActiveMode
        {
            get => _activeMode;
            set
            {
                _activeMode = value;
                GizmoModeChangedEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        public TransformSpace ActiveSpace
        {
            get => _activeSpace;
            set
            {
                _activeSpace = value;
                TransformSpaceChangedEvent?.Invoke(this, EventArgs.Empty);
            }
        }

        public PivotType ActivePivot = PivotType.SelectionCenter;

        // -- BoundingBoxes -- //

        #region BoundingBoxes

        private const float MULTI_AXIS_THICKNESS = 0.05f;
        private const float SINGLE_AXIS_THICKNESS = 0.2f;

        private static BoundingBox XAxisBox =>
            new(new Vector3(LINE_OFFSET, 0, 0),
                new Vector3(LINE_OFFSET + LINE_LENGTH, SINGLE_AXIS_THICKNESS, SINGLE_AXIS_THICKNESS));

        private static BoundingBox YAxisBox =>
            new(new Vector3(0, LINE_OFFSET, 0),
                new Vector3(SINGLE_AXIS_THICKNESS, LINE_OFFSET + LINE_LENGTH, SINGLE_AXIS_THICKNESS));

        private static BoundingBox ZAxisBox =>
            new(new Vector3(0, 0, LINE_OFFSET),
                new Vector3(SINGLE_AXIS_THICKNESS, SINGLE_AXIS_THICKNESS, LINE_OFFSET + LINE_LENGTH));

        private static BoundingBox XZAxisBox =>
            new(Vector3.Zero,
                new Vector3(LINE_OFFSET, MULTI_AXIS_THICKNESS, LINE_OFFSET));

        private BoundingBox XYBox =>
            new(Vector3.Zero,
                new Vector3(LINE_OFFSET, LINE_OFFSET, MULTI_AXIS_THICKNESS));

        private BoundingBox YZBox =>
            new(Vector3.Zero,
                new Vector3(MULTI_AXIS_THICKNESS, LINE_OFFSET, LINE_OFFSET));

        #endregion

        // -- BoundingSpheres -- //

        #region BoundingSpheres

        private const float RADIUS = 1f;

        private BoundingSphere XSphere =>
            new(Vector3.Transform(_translationLineVertices[1].Position, _gizmoWorld),
                RADIUS * _screenScale);

        private BoundingSphere YSphere =>
            new(Vector3.Transform(_translationLineVertices[7].Position, _gizmoWorld),
                RADIUS * _screenScale);

        private BoundingSphere ZSphere =>
            new(Vector3.Transform(_translationLineVertices[13].Position, _gizmoWorld),
                RADIUS * _screenScale);

        #endregion

        /// <summary>
        /// The value to adjust all transformation when precisionMode is active.
        /// </summary>
        private const float PRECISION_MODE_SCALE = 0.1f;

        // -- Selection -- //
        public readonly List<ITransformable> Selection = new();
        private IEnumerable<ITransformable> _selectionPool = null;

        private Vector3 _translationDelta = Vector3.Zero;
        private Matrix _rotationDelta = Matrix.Identity;
        private Vector3 _scaleDelta = Vector3.Zero;

        // -- Translation Variables -- //
        private Vector3 _tDelta;
        private Vector3 _lastIntersectionPosition;
        private Vector3 _intersectPosition;

        // copy
        private bool _copyActivated;

        public bool SnapEnabled = false;
        public bool PrecisionModeEnabled;
        public readonly float TranslationSnapValue = 5;
        public readonly float RotationSnapValue = 30;
        public readonly float ScaleSnapValue = 0.5f;

        private Vector3 _translationScaleSnapDelta;
        private float _rotationSnapDelta;

        private readonly BasicEffect _selectionBoxEffect;
        private readonly List<VertexPositionColor> _selectionBoxVertices = new();
        //public BoundingBox SelectionBox;

        //private LineRenderer _lineRenderer;
        //private bool _showLines;

        private KeyboardState _currentKeys;
        private MouseState _lastMouseState, _currentMouseState;

        public event EventHandler<List<ITransformable>> SelectionChanged;
        public event EventHandler<List<ITransformable>> CopyTriggered;

        public Gizmo(GraphicsDevice graphics)
            : this(graphics, Matrix.Identity) { }

        public Gizmo(GraphicsDevice graphics, Matrix world)
        {
            SceneWorld = world;
            _graphics = graphics;

            Enabled = true;

            _selectionBoxEffect = new BasicEffect(graphics) { VertexColorEnabled = true };
            _lineEffect = new BasicEffect(graphics) { VertexColorEnabled = true, AmbientLightColor = Vector3.One, EmissiveColor = Vector3.One };
            _meshEffect = new BasicEffect(graphics);
            _quadEffect = new BasicEffect(graphics) { World = Matrix.Identity, DiffuseColor = _highlightColor.ToVector3(), Alpha = 0.5f };
            _quadEffect.EnableDefaultLighting();

            Initialize();
        }

        private void Initialize()
        {
            // -- Set local-space offset -- //
            _modelLocalSpace = new Matrix[3];
            _modelLocalSpace[0] = Matrix.CreateWorld(new Vector3(LINE_LENGTH, 0, 0), Vector3.Left, Vector3.Up);
            _modelLocalSpace[1] = Matrix.CreateWorld(new Vector3(0, LINE_LENGTH, 0), Vector3.Down, Vector3.Left);
            _modelLocalSpace[2] = Matrix.CreateWorld(new Vector3(0, 0, LINE_LENGTH), Vector3.Forward, Vector3.Up);

            // -- Colors: X,Y,Z,Highlight -- //
            _axisColors = new Color[3];
            _axisColors[0] = Color.Red;
            _axisColors[1] = Color.Green;
            _axisColors[2] = Color.Blue;
            _highlightColor = Color.Gold;

            // text projected in 3D
            _axisText = new string[3];
            _axisText[0] = "X";
            _axisText[1] = "Y";
            _axisText[2] = "Z";

            // translucent quads

            #region Translucent Quads

            const float halfLineOffset = LINE_OFFSET / 2;
            _quads = new Quad[3];
            _quads[0] = new Quad(new Vector3(halfLineOffset, halfLineOffset, 0), Vector3.Backward, Vector3.Up, LINE_OFFSET,
                                 LINE_OFFSET); //XY
            _quads[1] = new Quad(new Vector3(halfLineOffset, 0, halfLineOffset), Vector3.Up, Vector3.Right, LINE_OFFSET,
                                 LINE_OFFSET); //XZ
            _quads[2] = new Quad(new Vector3(0, halfLineOffset, halfLineOffset), Vector3.Right, Vector3.Up, LINE_OFFSET,
                                 LINE_OFFSET); //ZY 

            #endregion

            // fill array with vertex-data

            #region Fill Axis-Line array

            var vertexList = new List<VertexPositionColor>(18);

            // helper to apply colors
            var xColor = _axisColors[0];
            var yColor = _axisColors[1];
            var zColor = _axisColors[2];


            // -- X Axis -- // index 0 - 5
            vertexList.Add(new VertexPositionColor(new Vector3(halfLineOffset, 0, 0), xColor));
            vertexList.Add(new VertexPositionColor(new Vector3(LINE_LENGTH, 0, 0), xColor));

            vertexList.Add(new VertexPositionColor(new Vector3(LINE_OFFSET, 0, 0), xColor));
            vertexList.Add(new VertexPositionColor(new Vector3(LINE_OFFSET, LINE_OFFSET, 0), xColor));

            vertexList.Add(new VertexPositionColor(new Vector3(LINE_OFFSET, 0, 0), xColor));
            vertexList.Add(new VertexPositionColor(new Vector3(LINE_OFFSET, 0, LINE_OFFSET), xColor));

            // -- Y Axis -- // index 6 - 11
            vertexList.Add(new VertexPositionColor(new Vector3(0, halfLineOffset, 0), yColor));
            vertexList.Add(new VertexPositionColor(new Vector3(0, LINE_LENGTH, 0), yColor));

            vertexList.Add(new VertexPositionColor(new Vector3(0, LINE_OFFSET, 0), yColor));
            vertexList.Add(new VertexPositionColor(new Vector3(LINE_OFFSET, LINE_OFFSET, 0), yColor));

            vertexList.Add(new VertexPositionColor(new Vector3(0, LINE_OFFSET, 0), yColor));
            vertexList.Add(new VertexPositionColor(new Vector3(0, LINE_OFFSET, LINE_OFFSET), yColor));

            // -- Z Axis -- // index 12 - 17
            vertexList.Add(new VertexPositionColor(new Vector3(0, 0, halfLineOffset), zColor));
            vertexList.Add(new VertexPositionColor(new Vector3(0, 0, LINE_LENGTH), zColor));

            vertexList.Add(new VertexPositionColor(new Vector3(0, 0, LINE_OFFSET), zColor));
            vertexList.Add(new VertexPositionColor(new Vector3(LINE_OFFSET, 0, LINE_OFFSET), zColor));

            vertexList.Add(new VertexPositionColor(new Vector3(0, 0, LINE_OFFSET), zColor));
            vertexList.Add(new VertexPositionColor(new Vector3(0, LINE_OFFSET, LINE_OFFSET), zColor));

            // -- Convert to array -- //
            _translationLineVertices = vertexList.ToArray();

            #endregion

            //const float length = 50;
            //_lineRenderer = new LineRenderer(_graphics, Color.Gold);
            //_lineRenderer.Add(Vector3.Backward*length, Vector3.Forward*length);
            //_lineRenderer.Add(Vector3.Up*length, Vector3.Down*length);
            //_lineRenderer.Add(Vector3.Left*length, Vector3.Right*length);
        }

        /// <summary>
        /// Sets a pool of ITransformable-objects for the gizmo.
        /// </summary>
        /// <param name="selectables"></param>
        public void SetSelectionPool(IEnumerable<ITransformable> selectables)
        {
            _selectionPool = selectables;
        }

        public IEnumerable<ITransformable> GetSelectionPool()
        {
            return _selectionPool;
        }

        //public void SetGizmoMode(GizmoMode mode)
        //{
        //  ActiveMode = mode;
        //}

        //public void SetTransformSpace(TransformSpace space)
        //{
        //  ActiveSpace = space;
        //}

        //public void SetPivotType(PivotType pivot)
        //{
        //  ActivePivot = pivot;
        //}

        public void NextPivotType()
        {
            if (ActivePivot == PivotType.WorldOrigin)
            {
                ActivePivot = PivotType.ObjectCenter;
            }
            else
            {
                ActivePivot++;
            }
        }

        public void SelectEntities(Vector2 mouseloc, bool addToSelection, bool removeFromSelection)
        {
            bool selectionChanged = false;

            if (ActiveAxis == GizmoAxis.None)
            {
                if (!addToSelection && !removeFromSelection)
                {
                    Selection.Clear();
                    selectionChanged = true;
                }

                selectionChanged |= PickObject(mouseloc, removeFromSelection);
            }
            ResetDeltas();

            if (selectionChanged)
            {
                RaiseSelectionChanged();
            }
        }

        /// <summary>
        /// Clears selection of Gizmo.
        /// </summary>
        public void Clear()
        {
            Selection?.Clear();
            RaiseSelectionChanged();
        }

        public void RaiseSelectionChanged()
        {
            SelectionChanged?.Invoke(this, Selection?.ToList());
        }

        protected void ResetDeltas()
        {
            _tDelta = Vector3.Zero;
            _lastIntersectionPosition = Vector3.Zero;
            _intersectPosition = Vector3.Zero;
        }

        public void Update(GameTime gameTime, KeyboardState keyboardState, MouseState mouseState)
        {
            _currentMouseState = mouseState;
            _currentKeys = keyboardState;

            var mousePosition = new Vector2(_currentMouseState.X, _currentMouseState.Y);

            // show or hide the orientation-lines helper.
            //_showLines = _currentKeys.IsKeyDown(Keys.Space);

            if (_isActive)
            {
                _lastIntersectionPosition = _intersectPosition;

                if (WasButtonHeld(MouseButtons.Left) && ActiveAxis != GizmoAxis.None)
                {
                    if (IsCopyKeyPressed())
                    {
                        if (!_copyActivated)
                        {
                            _copyActivated = true;
                            //Debug.WriteLine($"{DateTime.Now.TimeOfDay} copy {Selection.Count} entities");
                            CopyTriggered?.Invoke(this, Selection);
                        }
                    }
                    else
                    {
                        _copyActivated = false;
                    }

                    switch (ActiveMode)
                    {
                        case GizmoMode.UniformScale:
                        case GizmoMode.NonUniformScale:
                        case GizmoMode.Translate:
                            {
                                #region Translate & Scale

                                var delta = Vector3.Zero;
                                var ray = ConvertMouseToRay(mousePosition);

                                var transform = Matrix.Invert(_rotationMatrix);
                                ray.Position = Vector3.Transform(ray.Position, transform);
                                ray.Direction = Vector3.TransformNormal(ray.Direction, transform);


                                switch (ActiveAxis)
                                {
                                    case GizmoAxis.XY:
                                    case GizmoAxis.X:
                                        {
                                            var plane = new Plane(Vector3.Forward,
                                                                    Vector3.Transform(_position, Matrix.Invert(_rotationMatrix)).Z);

                                            var intersection = ray.Intersects(plane);
                                            if (intersection.HasValue)
                                            {
                                                _intersectPosition = (ray.Position + (ray.Direction * intersection.Value));
                                                if (_lastIntersectionPosition != Vector3.Zero)
                                                {
                                                    _tDelta = _intersectPosition - _lastIntersectionPosition;
                                                }
                                                delta = ActiveAxis == GizmoAxis.X
                                                          ? new Vector3(_tDelta.X, 0, 0)
                                                          : new Vector3(_tDelta.X, _tDelta.Y, 0);
                                            }
                                        }
                                        break;
                                    case GizmoAxis.Z:
                                    case GizmoAxis.YZ:
                                    case GizmoAxis.Y:
                                        {
                                            var plane = new Plane(Vector3.Left, Vector3.Transform(_position, Matrix.Invert(_rotationMatrix)).X);

                                            var intersection = ray.Intersects(plane);
                                            if (intersection.HasValue)
                                            {
                                                _intersectPosition = (ray.Position + (ray.Direction * intersection.Value));
                                                if (_lastIntersectionPosition != Vector3.Zero)
                                                {
                                                    _tDelta = _intersectPosition - _lastIntersectionPosition;
                                                }
                                                switch (ActiveAxis)
                                                {
                                                    case GizmoAxis.Y:
                                                        delta = new Vector3(0, _tDelta.Y, 0);
                                                        break;
                                                    case GizmoAxis.Z:
                                                        delta = new Vector3(0, 0, _tDelta.Z);
                                                        break;
                                                    default:
                                                        delta = new Vector3(0, _tDelta.Y, _tDelta.Z);
                                                        break;
                                                }
                                            }
                                        }
                                        break;
                                    case GizmoAxis.ZX:
                                        {
                                            var plane = new Plane(Vector3.Down, Vector3.Transform(_position, Matrix.Invert(_rotationMatrix)).Y);

                                            var intersection = ray.Intersects(plane);
                                            if (intersection.HasValue)
                                            {
                                                _intersectPosition = (ray.Position + (ray.Direction * intersection.Value));
                                                if (_lastIntersectionPosition != Vector3.Zero)
                                                {
                                                    _tDelta = _intersectPosition - _lastIntersectionPosition;
                                                }
                                            }
                                            delta = new Vector3(_tDelta.X, 0, _tDelta.Z);
                                        }
                                        break;
                                }


                                if (SnapEnabled)
                                {
                                    var snapValue = TranslationSnapValue;
                                    if (ActiveMode == GizmoMode.UniformScale || ActiveMode == GizmoMode.NonUniformScale)
                                    {
                                        snapValue = ScaleSnapValue;
                                    }

                                    if (PrecisionModeEnabled)
                                    {
                                        delta *= PRECISION_MODE_SCALE;
                                        snapValue *= PRECISION_MODE_SCALE;
                                    }

                                    _translationScaleSnapDelta += delta;

                                    delta = new Vector3(
                                      (int)(_translationScaleSnapDelta.X / snapValue) * snapValue,
                                      (int)(_translationScaleSnapDelta.Y / snapValue) * snapValue,
                                      (int)(_translationScaleSnapDelta.Z / snapValue) * snapValue);

                                    _translationScaleSnapDelta -= delta;
                                }
                                else if (PrecisionModeEnabled)
                                {
                                    delta *= PRECISION_MODE_SCALE;
                                }


                                if (ActiveMode == GizmoMode.Translate)
                                {
                                    // transform (local or world)
                                    delta = Vector3.Transform(delta, _rotationMatrix);
                                    _translationDelta = delta;
                                }
                                else if (ActiveMode == GizmoMode.NonUniformScale || ActiveMode == GizmoMode.UniformScale)
                                {
                                    // -- Apply Scale -- //
                                    _scaleDelta += delta;
                                }
                                #endregion
                            }
                            break;
                        case GizmoMode.Rotate:
                            {
                                #region Rotate

                                var delta = mousePosition.X - _lastMouseState.X;
                                delta *= (float)gameTime.ElapsedGameTime.TotalSeconds;

                                if (SnapEnabled)
                                {
                                    var snapValue = MathHelper.ToRadians(RotationSnapValue);
                                    if (PrecisionModeEnabled)
                                    {
                                        delta *= PRECISION_MODE_SCALE;
                                        snapValue *= PRECISION_MODE_SCALE;
                                    }

                                    _rotationSnapDelta += delta;

                                    var snapped = (int)(_rotationSnapDelta / snapValue) * snapValue;
                                    _rotationSnapDelta -= snapped;

                                    delta = snapped;
                                }
                                else if (PrecisionModeEnabled)
                                {
                                    delta *= PRECISION_MODE_SCALE;
                                }

                                // rotation matrix to transform - if more than one objects selected, always use world-space.
                                var rot = Matrix.Identity;
                                rot.Forward = SceneWorld.Forward;
                                rot.Up = SceneWorld.Up;
                                rot.Right = SceneWorld.Right;

                                switch (ActiveAxis)
                                {
                                    case GizmoAxis.X:
                                        rot *= Matrix.CreateFromAxisAngle(_rotationMatrix.Right, delta);
                                        break;
                                    case GizmoAxis.Y:
                                        rot *= Matrix.CreateFromAxisAngle(_rotationMatrix.Up, delta);
                                        break;
                                    case GizmoAxis.Z:
                                        rot *= Matrix.CreateFromAxisAngle(_rotationMatrix.Forward, delta);
                                        break;
                                }
                                _rotationDelta = rot;

                                #endregion
                            }
                            break;
                    }
                }
                else
                {
                    if (_currentMouseState.LeftButton == ButtonState.Released &&
                        _currentMouseState.RightButton == ButtonState.Released)
                    {
                        SelectAxis(mousePosition);
                    }

                    _copyActivated = false;
                }

                SetGizmoPosition();

                if (WasButtonHeld(MouseButtons.Left))
                {
                    if (_translationDelta != Vector3.Zero)
                    {
                        foreach (var entity in Selection)
                        {
                            OnTranslateEvent(entity, _translationDelta);
                        }

                        _translationDelta = Vector3.Zero;
                    }
                    if (_rotationDelta != Matrix.Identity)
                    {
                        foreach (var entity in Selection)
                        {
                            OnRotateEvent(entity, _rotationDelta);
                        }

                        _rotationDelta = Matrix.Identity;
                    }
                    if (_scaleDelta != Vector3.Zero)
                    {
                        foreach (var entity in Selection)
                        {
                            OnScaleEvent(entity, _scaleDelta);
                        }

                        _scaleDelta = Vector3.Zero;
                    }
                }
            }

            _lastMouseState = _currentMouseState;

            if (Selection.Count < 1)
            {
                _isActive = false;
                ActiveAxis = GizmoAxis.None;
                return;
            }
            // helps solve visual lag (1-frame-lag) after selecting a new entity
            if (!_isActive)
            {
                SetGizmoPosition();
            }

            _isActive = true;

            // -- Scale Gizmo to fit on-screen -- //
            var vLength = _cameraPosition - _position;
            const float scaleFactor = 25;

            _screenScale = vLength.Length() / scaleFactor;
            _screenScaleMatrix = Matrix.CreateScale(new Vector3(_screenScale));

            _localForward = Selection[0].Forward;
            _localUp = Selection[0].Up;
            // -- Vector Rotation (Local/World) -- //
            _localForward.Normalize();
            _localRight = Vector3.Cross(_localForward, _localUp);
            _localUp = Vector3.Cross(_localRight, _localForward);
            _localRight.Normalize();
            _localUp.Normalize();

            // -- Create Both World Matrices -- //
            _objectOrientedWorld = _screenScaleMatrix * Matrix.CreateWorld(_position, _localForward, _localUp);
            _axisAlignedWorld = _screenScaleMatrix * Matrix.CreateWorld(_position, SceneWorld.Forward, SceneWorld.Up);

            // Assign World
            if (ActiveSpace == TransformSpace.World ||
                ActiveMode == GizmoMode.Rotate ||
                ActiveMode == GizmoMode.NonUniformScale ||
                ActiveMode == GizmoMode.UniformScale)
            {
                _gizmoWorld = _axisAlignedWorld;

                // align lines, boxes etc. with the grid-lines
                _rotationMatrix.Forward = SceneWorld.Forward;
                _rotationMatrix.Up = SceneWorld.Up;
                _rotationMatrix.Right = SceneWorld.Right;
            }
            else
            {
                _gizmoWorld = _objectOrientedWorld;

                // align lines, boxes etc. with the selected object
                _rotationMatrix.Forward = _localForward;
                _rotationMatrix.Up = _localUp;
                _rotationMatrix.Right = _localRight;
            }

            // -- Reset Colors to default -- //
            ApplyColor(GizmoAxis.X, _axisColors[0]);
            ApplyColor(GizmoAxis.Y, _axisColors[1]);
            ApplyColor(GizmoAxis.Z, _axisColors[2]);

            // -- Apply Highlight -- //
            ApplyColor(ActiveAxis, _highlightColor);

        }

        #region Input Helpers
        /// <summary>
        /// Returns true is any of the modifier keys is pressed.
        /// </summary>
        /// <returns></returns>
        //private bool IsAnyModifierPressed()
        //{
        //    return _currentKeys.IsKeyDown(Keys.LeftControl) || _currentKeys.IsKeyDown(Keys.RightControl) ||
        //           _currentKeys.IsKeyDown(Keys.LeftShift) || _currentKeys.IsKeyDown(Keys.RightShift) ||
        //           _currentKeys.IsKeyDown(Keys.LeftAlt) || _currentKeys.IsKeyDown(Keys.RightAlt);
        //}

        private bool IsCopyKeyPressed()
        {
            return _currentKeys.IsKeyDown(Keys.LeftControl) || _currentKeys.IsKeyDown(Keys.RightControl);
        }

        private bool WasButtonHeld(MouseButtons button)
        {
            return (GetButtonState(button, _currentMouseState) == ButtonState.Pressed
                    && GetButtonState(button, _lastMouseState) == ButtonState.Pressed);
        }

        private ButtonState GetButtonState(MouseButtons button, MouseState state)
        {
            if (button == MouseButtons.Left)
            {
                return state.LeftButton;
            }

            if (button == MouseButtons.Middle)
            {
                return state.MiddleButton;
            }

            if (button == MouseButtons.Right)
            {
                return state.RightButton;
            }

            if (button == MouseButtons.X1)
            {
                return state.XButton1;
            }

            if (button == MouseButtons.X2)
            {
                return state.XButton2;
            }

            return ButtonState.Released;
        }
        #endregion


        /// <summary>
        /// Helper method for applying color to the gizmo lines.
        /// </summary>
        private void ApplyColor(GizmoAxis axis, Color color)
        {
            switch (ActiveMode)
            {
                case GizmoMode.NonUniformScale:
                case GizmoMode.Translate:
                    switch (axis)
                    {
                        case GizmoAxis.X:
                            ApplyLineColor(0, 6, color);
                            break;
                        case GizmoAxis.Y:
                            ApplyLineColor(6, 6, color);
                            break;
                        case GizmoAxis.Z:
                            ApplyLineColor(12, 6, color);
                            break;
                        case GizmoAxis.XY:
                            ApplyLineColor(0, 4, color);
                            ApplyLineColor(6, 4, color);
                            break;
                        case GizmoAxis.YZ:
                            ApplyLineColor(6, 2, color);
                            ApplyLineColor(12, 2, color);
                            ApplyLineColor(10, 2, color);
                            ApplyLineColor(16, 2, color);
                            break;
                        case GizmoAxis.ZX:
                            ApplyLineColor(0, 2, color);
                            ApplyLineColor(4, 2, color);
                            ApplyLineColor(12, 4, color);
                            break;
                    }
                    break;
                case GizmoMode.Rotate:
                    switch (axis)
                    {
                        case GizmoAxis.X:
                            ApplyLineColor(0, 6, color);
                            break;
                        case GizmoAxis.Y:
                            ApplyLineColor(6, 6, color);
                            break;
                        case GizmoAxis.Z:
                            ApplyLineColor(12, 6, color);
                            break;
                    }
                    break;
                case GizmoMode.UniformScale:
                    ApplyLineColor(0, _translationLineVertices.Length,
                                   ActiveAxis == GizmoAxis.None ? _axisColors[0] : _highlightColor);
                    break;
            }
        }

        /// <summary>
        /// Apply color on the lines associated with translation mode (re-used in Scale)
        /// </summary>
        private void ApplyLineColor(int startindex, int count, Color color)
        {
            for (var i = startindex; i < (startindex + count); i++)
            {
                _translationLineVertices[i].Color = color;
            }
        }

        /// <summary>
        /// Per-frame check to see if mouse is hovering over any axis.
        /// </summary>
        private void SelectAxis(Vector2 mousePosition)
        {
            if (!Enabled)
            {
                return;
            }

            var closestintersection = float.MaxValue;
            var ray = ConvertMouseToRay(mousePosition);

            if (ActiveMode == GizmoMode.Translate)
            {
                // transform ray into local-space of the boundingboxes.
                ray.Direction = Vector3.TransformNormal(ray.Direction, Matrix.Invert(_gizmoWorld));
                ray.Position = Vector3.Transform(ray.Position, Matrix.Invert(_gizmoWorld));
            }

            #region X,Y,Z Boxes
            var intersection = XAxisBox.Intersects(ray);
            if (intersection < closestintersection)
            {
                ActiveAxis = GizmoAxis.X;
                closestintersection = intersection.Value;
            }

            intersection = YAxisBox.Intersects(ray);
            if (intersection < closestintersection)
            {
                ActiveAxis = GizmoAxis.Y;
                closestintersection = intersection.Value;
            }
            intersection = ZAxisBox.Intersects(ray);
            if (intersection < closestintersection)
            {
                ActiveAxis = GizmoAxis.Z;
                closestintersection = intersection.Value;
            }
            #endregion

            if (ActiveMode == GizmoMode.Rotate || ActiveMode == GizmoMode.UniformScale ||
                ActiveMode == GizmoMode.NonUniformScale)
            {
                #region BoundingSpheres

                intersection = XSphere.Intersects(ray);
                if (intersection < closestintersection)
                {
                    ActiveAxis = GizmoAxis.X;
                    closestintersection = intersection.Value;
                }

                intersection = YSphere.Intersects(ray);
                if (intersection < closestintersection)
                {
                    ActiveAxis = GizmoAxis.Y;
                    closestintersection = intersection.Value;
                }

                intersection = ZSphere.Intersects(ray);
                if (intersection < closestintersection)
                {
                    ActiveAxis = GizmoAxis.Z;
                    closestintersection = intersection.Value;
                }

                #endregion
            }
            if (ActiveMode == GizmoMode.Translate || ActiveMode == GizmoMode.NonUniformScale ||
                ActiveMode == GizmoMode.UniformScale)
            {
                // if no axis was hit (x,y,z) set value to lowest possible to select the 'farthest' intersection for the XY,XZ,YZ boxes. 
                // This is done so you may still select multi-axis if you're looking at the gizmo from behind!
                if (closestintersection >= float.MaxValue)
                {
                    closestintersection = float.MinValue;
                }

                #region BoundingBoxes
                intersection = XYBox.Intersects(ray);
                if (intersection > closestintersection)
                {
                    ActiveAxis = GizmoAxis.XY;
                    closestintersection = intersection.Value;
                }

                intersection = XZAxisBox.Intersects(ray);
                if (intersection > closestintersection)
                {
                    ActiveAxis = GizmoAxis.ZX;
                    closestintersection = intersection.Value;
                }

                intersection = YZBox.Intersects(ray);
                if (intersection > closestintersection)
                {
                    ActiveAxis = GizmoAxis.YZ;
                    closestintersection = intersection.Value;
                }

                #endregion
            }
            if (closestintersection >= float.MaxValue || closestintersection <= float.MinValue)
            {
                ActiveAxis = GizmoAxis.None;
            }
        }

        /// <summary>
        /// Select objects inside the scene.
        /// </summary>
        protected bool PickObject(Vector2 mousePosition, bool removeFromSelection)
        {
            if (_selectionPool == null)
            {
                throw new Exception("SelectionPool is null, please set the pool by calling .SetSelectionPool()");
            }

            var selectionChanged = false;
            var ray = ConvertMouseToRay(mousePosition);
            var closest = float.MaxValue;
            ITransformable obj = null;
            foreach (var entity in _selectionPool)
            {
                var intersection = ray.Intersects(entity.BoundingBox);

                if (intersection < closest)
                {
                    if (!Selection.Contains(entity))
                    {
                        obj = entity;
                        closest = intersection.Value;
                    }
                    if (removeFromSelection)
                    {
                        Selection.Remove(entity);
                        selectionChanged = true;
                    }
                }
            }
            if (obj != null)
            {
                Selection.Add(obj);
                selectionChanged = true;
            }

            return selectionChanged;
        }

        /// <summary>
        /// Set position of the gizmo, position will be center of all selected entities.
        /// </summary>
        private void SetGizmoPosition()
        {
            switch (ActivePivot)
            {
                case PivotType.ObjectCenter:
                    if (Selection.Count > 0)
                    {
                        _position = Selection[0].Position;
                    }

                    break;
                case PivotType.SelectionCenter:
                    _position = GetSelectionCenter();
                    break;
                case PivotType.WorldOrigin:
                    _position = SceneWorld.Translation;
                    break;
            }
            _position += _translationDelta;
        }

        /// <summary>
        /// Returns center position of all selected objectes.
        /// </summary>
        /// <returns></returns>
        private Vector3 GetSelectionCenter()
        {
            if (Selection.Count == 0)
            {
                return Vector3.Zero;
            }

            var center = Vector3.Zero;
            foreach (var selected in Selection)
            {
                center += selected.Position;
            }

            return center / Selection.Count;
        }

        public void UpdateCameraProperties(Matrix view, Matrix projection, Vector3 cameraPosition)
        {
            _view = view;
            _projection = projection;
            _cameraPosition = cameraPosition;
        }

        #region Draw
        public void Draw()
        {
            if (!_isActive)
            {
                return;
            }

            _graphics.DepthStencilState = DepthStencilState.None;

            if (_view == Matrix.Identity || _projection == Matrix.Identity)
            {
                throw new Exception("Error: Must call .UpdateCameraProperties() before .Draw()");
            }

            #region Draw: Axis-Lines

            // -- Draw Lines -- //
            _lineEffect.World = _gizmoWorld;
            _lineEffect.View = _view;
            _lineEffect.Projection = _projection;

            _lineEffect.CurrentTechnique.Passes[0].Apply();
            _graphics.DrawUserPrimitives(PrimitiveType.LineList, _translationLineVertices, 0,
                                         _translationLineVertices.Length / 2);

            #endregion

            switch (ActiveMode)
            {
                case GizmoMode.NonUniformScale:
                case GizmoMode.Translate:
                    switch (ActiveAxis)
                    {
                        #region Draw Quads
                        case GizmoAxis.ZX:
                        case GizmoAxis.YZ:
                        case GizmoAxis.XY:
                            {
                                _graphics.BlendState = BlendState.AlphaBlend;
                                _graphics.RasterizerState = RasterizerState.CullNone;

                                _quadEffect.World = _gizmoWorld;
                                _quadEffect.View = _view;
                                _quadEffect.Projection = _projection;

                                _quadEffect.CurrentTechnique.Passes[0].Apply();

                                var activeQuad = new Quad();
                                switch (ActiveAxis)
                                {
                                    case GizmoAxis.XY:
                                        activeQuad = _quads[0];
                                        break;
                                    case GizmoAxis.ZX:
                                        activeQuad = _quads[1];
                                        break;
                                    case GizmoAxis.YZ:
                                        activeQuad = _quads[2];
                                        break;
                                }

                                _graphics.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                                                                    activeQuad.Vertices, 0, 4,
                                                                    activeQuad.Indexes, 0, 2);

                                _graphics.BlendState = BlendState.Opaque;
                                _graphics.RasterizerState = RasterizerState.CullCounterClockwise;
                            }
                            break;
                            #endregion
                    }
                    break;
                case GizmoMode.UniformScale:
                    #region Draw Quads
                    if (ActiveAxis != GizmoAxis.None)
                    {
                        _graphics.BlendState = BlendState.AlphaBlend;
                        _graphics.RasterizerState = RasterizerState.CullNone;

                        _quadEffect.World = _gizmoWorld;
                        _quadEffect.View = _view;
                        _quadEffect.Projection = _projection;
                        _quadEffect.CurrentTechnique.Passes[0].Apply();

                        for (var i = 0; i < _quads.Length; i++)
                        {
                            _graphics.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                                _quads[i].Vertices, 0, 4,
                                _quads[i].Indexes, 0, 2);
                        }

                        _graphics.BlendState = BlendState.Opaque;
                        _graphics.RasterizerState = RasterizerState.CullCounterClockwise;
                    }
                    #endregion
                    break;
            }

            // draw the 3d meshes
            for (var i = 0; i < 3; i++) //(order: x, y, z)
            {
                GizmoModel activeModel;
                switch (ActiveMode)
                {
                    case GizmoMode.Translate:
                        activeModel = Geometry.Translate;
                        break;
                    case GizmoMode.Rotate:
                        activeModel = Geometry.Rotate;
                        break;
                    default:
                        activeModel = Geometry.Scale;
                        break;
                }

                Vector3 color;
                switch (ActiveMode)
                {
                    case GizmoMode.UniformScale:
                        color = _axisColors[0].ToVector3();
                        break;
                    default:
                        color = _axisColors[i].ToVector3();
                        break;
                }

                _meshEffect.World = _modelLocalSpace[i] * _gizmoWorld;
                _meshEffect.View = _view;
                _meshEffect.Projection = _projection;

                _meshEffect.DiffuseColor = color;
                _meshEffect.EmissiveColor = color;

                _meshEffect.CurrentTechnique.Passes[0].Apply();

                _graphics.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    activeModel.Vertices, 0, activeModel.Vertices.Length,
                    activeModel.Indices, 0, activeModel.Indices.Length / 3);
            }
            //foreach (ModelMesh mesh in activeModel.Meshes)
            //{
            //  foreach (ModelMeshPart meshpart in mesh.MeshParts)
            //  {
            //    BasicEffect effect = (BasicEffect) meshpart.Effect;
            //    Vector3 color;
            //    switch (ActiveMode)
            //    {
            //      case GizmoMode.UniformScale:
            //        color = _axisColors[0].ToVector3();
            //        break;
            //      default:
            //        color = _axisColors[i].ToVector3();
            //        break;
            //    }


            //    effect.World = _modelLocalSpace[i]*_gizmoWorld;
            //    effect.DiffuseColor = color;
            //    effect.EmissiveColor = color;

            //    effect.EnableDefaultLighting();

            //    effect.View = _view;
            //    effect.Projection = _projection;
            //  }
            //  mesh.Draw();
            //}

            if (SelectionBoxesIsVisible)
            {
                DrawSelectionBox();
            }

            _graphics.DepthStencilState = DepthStencilState.Default;

            //if (_showLines)
            //  _lineRenderer.Draw(_gizmoWorld);

            Draw2D();
        }

        private void Draw2D()
        {
            //_spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // -- Draw Axis identifiers ("X,Y,Z") -- // 
            for (var i = 0; i < 3; i++)
            {
                var screenPos =
                  _graphics.Viewport.Project(_modelLocalSpace[i].Translation + _modelLocalSpace[i].Backward + _axisTextOffset,
                                             _projection, _view, _gizmoWorld);

                if (screenPos.Z < 0f || screenPos.Z > 1.0f)
                {
                    continue;
                }

                var color = _axisColors[i];
                switch (i)
                {
                    case 0:
                        if (ActiveAxis == GizmoAxis.X || ActiveAxis == GizmoAxis.XY || ActiveAxis == GizmoAxis.ZX)
                        {
                            color = _highlightColor;
                        }

                        break;
                    case 1:
                        if (ActiveAxis == GizmoAxis.Y || ActiveAxis == GizmoAxis.XY || ActiveAxis == GizmoAxis.YZ)
                        {
                            color = _highlightColor;
                        }

                        break;
                    case 2:
                        if (ActiveAxis == GizmoAxis.Z || ActiveAxis == GizmoAxis.YZ || ActiveAxis == GizmoAxis.ZX)
                        {
                            color = _highlightColor;
                        }

                        break;
                }

                //_spriteBatch.DrawString(_font, _axisText[i], new Vector2(screenPos.X, screenPos.Y), color);
            }

            // -- Draw StatusInfo -- //
            /*var statusInfo = GetStatusInfo();
            var stringDims = _font.MeasureString(statusInfo);
            var position = new Vector2(_graphics.Viewport.Width - stringDims.X, _graphics.Viewport.Height - stringDims.Y);

            _spriteBatch.DrawString(_font, statusInfo, position, Color.White);
            _spriteBatch.End();*/
        }

        /// <summary>
        /// returns a string filled with status info of the gizmo component. (includes: mode/space/snapping/precision/pivot)
        /// </summary>
        /// <returns></returns>
        private string GetStatusInfo()
        {
            return "Mode: " + ActiveMode + " | Space: " + ActiveSpace + " | Snapping:" +
                   (SnapEnabled ? "ON" : "OFF") +
                   " | Precision:" + (PrecisionModeEnabled ? "ON" : "OFF") + " | Pivot: " + ActivePivot + " ";
        }
        #endregion

        #region ConvertMouseToRay
        /// <summary>
        /// Converts the 2D mouse position to a 3D ray for collision tests.
        /// </summary>
        private Ray ConvertMouseToRay(Vector2 mousePosition)
        {
            var nearPoint = new Vector3(mousePosition, 0);
            var farPoint = new Vector3(mousePosition, 1);

            nearPoint = _graphics.Viewport.Unproject(nearPoint,
                                                     _projection,
                                                     _view,
                                                     Matrix.Identity);
            farPoint = _graphics.Viewport.Unproject(farPoint,
                                                    _projection,
                                                    _view,
                                                    Matrix.Identity);

            var direction = farPoint - nearPoint;
            direction.Normalize();

            return new Ray(nearPoint, direction);
        }
        #endregion

        #region Selection Box
        private void CreateSelectionBox()
        {
            var lineColor = Color.White;
            const float lineLength = 5f;

            _selectionBoxVertices.Clear();

            //Vector3 min = new Vector3(float.MaxValue), max = new Vector3(float.MinValue);
            //SelectionBox = new BoundingBox();
            //foreach (var selectable in Selection)
            //{
            //  min = Vector3.Min(min, selectable.BoundingBox.Min);
            //  max = Vector3.Max(max, selectable.BoundingBox.Max);
            //}

            //// convert to local-space
            //min -= _position;
            //max -= _position;

            //SelectionBox = new BoundingBox(min, max);
            //Vector3[] boundingBoxCorners = SelectionBox.GetCorners();

            foreach (var transformable in Selection)
            {
                var boundingBox = transformable.BoundingBox;
                var boundingBoxCorners = boundingBox.GetCorners();

                #region Create Corners
                // --- Corner 0 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[0], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[0] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[3].Y - boundingBoxCorners[0].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[0], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[0] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[4].Z - boundingBoxCorners[0].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[0], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[0] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[1].X - boundingBoxCorners[0].X) / lineLength,
                                                                    0, 0), lineColor));


                // --- Corner 1 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[1], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[1] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[2].Y - boundingBoxCorners[1].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[1], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[1] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[5].Z - boundingBoxCorners[1].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[1], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[1] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[0].X - boundingBoxCorners[1].X) / lineLength,
                                                                    0, 0), lineColor));


                // --- Corner 2 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[2], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[2] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[1].Y - boundingBoxCorners[2].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[2], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[2] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[6].Z - boundingBoxCorners[2].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[2], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[2] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[3].X - boundingBoxCorners[2].X) / lineLength,
                                                                    0, 0), lineColor));


                // --- Corner 3 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[3], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[3] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[0].Y - boundingBoxCorners[3].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[3], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[3] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[7].Z - boundingBoxCorners[3].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[3], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[3] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[2].X - boundingBoxCorners[3].X) / lineLength,
                                                                    0, 0), lineColor));


                // --- Corner 4 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[4], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[4] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[7].Y - boundingBoxCorners[4].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[4], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[4] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[0].Z - boundingBoxCorners[4].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[4], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[4] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[5].X - boundingBoxCorners[4].X) / lineLength,
                                                                    0, 0), lineColor));


                // --- Corner 5 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[5], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[5] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[6].Y - boundingBoxCorners[5].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[5], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[5] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[1].Z - boundingBoxCorners[5].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[5], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[5] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[4].X - boundingBoxCorners[5].X) / lineLength,
                                                                    0, 0), lineColor));

                // --- Corner 6 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[6], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[6] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[5].Y - boundingBoxCorners[6].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[6], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[6] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[2].Z - boundingBoxCorners[6].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[6], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[6] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[7].X - boundingBoxCorners[6].X) / lineLength,
                                                                    0, 0), lineColor));


                // --- Corner 7 --- // 
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[7], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[7] +
                                                                  new Vector3(0,
                                                                              (boundingBoxCorners[4].Y - boundingBoxCorners[7].Y) /
                                                                              lineLength, 0), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[7], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[7] +
                                                                  new Vector3(0, 0,
                                                                              (boundingBoxCorners[3].Z - boundingBoxCorners[7].Z) /
                                                                              lineLength), lineColor));

                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[7], lineColor));
                _selectionBoxVertices.Add(new VertexPositionColor(boundingBoxCorners[7] +
                                                                  new Vector3(
                                                                    (boundingBoxCorners[6].X - boundingBoxCorners[7].X) / lineLength,
                                                                    0, 0), lineColor));
                #endregion
            }
        }

        private void DrawSelectionBox()
        {
            CreateSelectionBox();

            const float boxScale = 1.02f;

            if (Selection.Count > 0)
            {
                _selectionBoxEffect.View = _view;
                _selectionBoxEffect.Projection = _projection;
                _selectionBoxEffect.World = Matrix.Identity;

                _selectionBoxEffect.CurrentTechnique.Passes[0].Apply();
                _graphics.DrawUserPrimitives(PrimitiveType.LineList, _selectionBoxVertices.ToArray(), 0,
                                             _selectionBoxVertices.Count / 2);
            }
        }
        #endregion

        #region Event Triggers
        public event TransformationEventHandler TranslateEvent;
        public event TransformationEventHandler RotateEvent;
        public event TransformationEventHandler ScaleEvent;

        public event EventHandler GizmoModeChangedEvent;
        public event EventHandler TransformSpaceChangedEvent;

        private void OnTranslateEvent(ITransformable transformable, Vector3 delta)
        {
            TranslateEvent?.Invoke(transformable, new TransformationEventArgs(delta));
        }

        private void OnRotateEvent(ITransformable transformable, Matrix delta)
        {
            RotateEvent?.Invoke(transformable, new TransformationEventArgs(delta));
        }

        private void OnScaleEvent(ITransformable transformable, Vector3 delta)
        {
            ScaleEvent?.Invoke(transformable, new TransformationEventArgs(delta));
        }

        #endregion

        #region Private Quad Struct

        private struct Quad
        {
            public Vector3 Origin;
            public readonly Vector3 UpperLeft;
            public readonly Vector3 LowerLeft;
            public readonly Vector3 UpperRight;
            public readonly Vector3 LowerRight;
            public readonly Vector3 Normal;
            public readonly Vector3 Up;
            public readonly Vector3 Left;

            public readonly VertexPositionNormalTexture[] Vertices;
            public readonly short[] Indexes;

            public Quad(Vector3 origin, Vector3 normal, Vector3 up,
                        float width, float height)
            {
                Vertices = new VertexPositionNormalTexture[4];
                Indexes = new short[6];
                Origin = origin;
                Normal = normal;
                Up = up;

                // Calculate the quad corners
                Left = Vector3.Cross(normal, Up);
                var uppercenter = (Up * height / 2) + origin;
                UpperLeft = uppercenter + (Left * width / 2);
                UpperRight = uppercenter - (Left * width / 2);
                LowerLeft = UpperLeft - (Up * height);
                LowerRight = UpperRight - (Up * height);

                FillVertices();
            }

            private void FillVertices()
            {
                // Fill in texture coordinates to display full texture
                // on quad
                var textureUpperLeft = new Vector2(0.0f, 0.0f);
                var textureUpperRight = new Vector2(1.0f, 0.0f);
                var textureLowerLeft = new Vector2(0.0f, 1.0f);
                var textureLowerRight = new Vector2(1.0f, 1.0f);

                // Provide a normal for each vertex
                for (var i = 0; i < Vertices.Length; i++)
                {
                    Vertices[i].Normal = Normal;
                }

                // Set the position and texture coordinate for each
                // vertex
                Vertices[0].Position = LowerLeft;
                Vertices[0].TextureCoordinate = textureLowerLeft;
                Vertices[1].Position = UpperLeft;
                Vertices[1].TextureCoordinate = textureUpperLeft;
                Vertices[2].Position = LowerRight;
                Vertices[2].TextureCoordinate = textureLowerRight;
                Vertices[3].Position = UpperRight;
                Vertices[3].TextureCoordinate = textureUpperRight;

                // Set the index buffer for each vertex, using
                // clockwise winding
                Indexes[0] = 0;
                Indexes[1] = 1;
                Indexes[2] = 2;
                Indexes[3] = 2;
                Indexes[4] = 1;
                Indexes[5] = 3;
            }
        }
        #endregion

        #region Helper Functions
        /// <summary>
        /// Helper function to apply rotation to objects using the built-in method.
        /// </summary>
        public void RotationHelper(ITransformable entity, TransformationEventArgs e)
        {
            var pos = _position;
            if (ActivePivot == PivotType.ObjectCenter)
            {
                pos = entity.Position;
            }

            var localRot = Matrix.Identity;
            localRot.Forward = entity.Forward;
            localRot.Up = entity.Up;
            localRot.Right = Vector3.Cross(entity.Forward, entity.Up);
            localRot.Right.Normalize();
            localRot.Translation = entity.Position - pos;

            var newRot = localRot * (Matrix)e.Value;

#if USE_QUATERNION
            entity.Orientation = Quaternion.CreateFromRotationMatrix(newRot);
#elif USE_ROTATIONMATRIX
            entity.Rotation = newRot;
#else
            entity.Forward = newRot.Forward;
            entity.Up = newRot.Up;
#endif
            entity.Position = newRot.Translation + pos;
        }

        public void ToggleActiveSpace()
        {
            ActiveSpace = ActiveSpace == TransformSpace.Local ? TransformSpace.World : TransformSpace.Local;
        }

        #endregion
    }

    // An enum of buttons on the mouse, since XNA doesn't provide one
    internal enum MouseButtons { Left, Right, Middle, X1, X2 };

    #region Gizmo EventHandlers

    public class TransformationEventArgs
    {
        public readonly ValueType Value;

        public TransformationEventArgs(ValueType value)
        {
            Value = value;
        }
    }
    public delegate void TransformationEventHandler(ITransformable transformable, TransformationEventArgs e);

    #endregion

    #region Gizmo Enums

    public enum GizmoAxis
    {
        X,
        Y,
        Z,
        XY,
        ZX,
        YZ,
        None
    }

    public enum GizmoMode
    {
        Translate,
        Rotate,
        NonUniformScale,
        UniformScale
    }

    public enum TransformSpace
    {
        Local,
        World
    }

    public enum PivotType
    {
        ObjectCenter,
        SelectionCenter,
        WorldOrigin
    }

    #endregion
}
