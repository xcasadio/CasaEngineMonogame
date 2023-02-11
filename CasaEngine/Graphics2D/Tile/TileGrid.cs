//-----------------------------------------------------------------------------
// TileGrid.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;


namespace CasaEngine.Graphics2D.Tile
{
    public class TileGrid
        : TileLayer
    {

        private readonly int[][] _grid;
        //private SpriteSheet sheet;
        private readonly int _width;
        private readonly int _height;
        private readonly int _cellWidth;
        private readonly int _cellHeight;
        private Rectangle _visibleTiles;

        private Vector2 _scaleValue;



        public Vector2 TileScale
        {
            set
            {
                _scaleValue = value;
                VisibilityChanged = true;
            }
            get => _scaleValue;
        }



        public TileGrid(int tileWidth, int tileHeight, int numXTiles, int numYTiles,
            Vector2 offset,  /*SpriteSheet tileSheet,*/
            GraphicsDeviceManager graphicsComponent/*, Renderer2DComponent Renderer2DComponent_*/)
            : base(offset, graphicsComponent/*, Renderer2DComponent_*/)
        {
            //sheet = tileSheet;
            _width = numXTiles;
            _height = numYTiles;
            _cellWidth = tileWidth;
            _cellHeight = tileHeight;

            _scaleValue = Vector2.One;

            _visibleTiles = new Rectangle(0, 0, _width, _height);

            _grid = new int[_width][];
            for (int i = 0; i < _width; i++)
            {
                _grid[i] = new int[_height];
                for (int j = 0; j < _height; j++)
                {
                    _grid[i][j] = 0;
                }
            }
        }



        public void SetTile(int xIndex, int yIndex, int tile)
        {
            _grid[xIndex][yIndex] = tile;
        }


        protected override void DetermineVisibility()
        {
            //create the view rectangle
            Vector2 upperLeft = Vector2.Zero;
            Vector2 upperRight = Vector2.Zero;
            Vector2 lowerLeft = Vector2.Zero;
            Vector2 lowerRight = Vector2.Zero;
            lowerRight.X = ((DisplaySize.X / 2) / CameraZoom);
            lowerRight.Y = ((DisplaySize.Y / 2) / CameraZoom);
            upperRight.X = lowerRight.X;
            upperRight.Y = -lowerRight.Y;
            lowerLeft.X = -lowerRight.X;
            lowerLeft.Y = lowerRight.Y;
            upperLeft.X = -lowerRight.X;
            upperLeft.Y = -lowerRight.Y;


            //rotate the view rectangle appropriately
            Matrix rot = RotationMatrix;
            Vector2.Transform(ref upperLeft, ref rot, out upperLeft);
            Vector2.Transform(ref lowerRight, ref rot, out lowerRight);
            Vector2.Transform(ref upperRight, ref rot, out upperRight);
            Vector2.Transform(ref lowerLeft, ref rot, out lowerLeft);

            lowerLeft += (CameraPosition);
            lowerRight += (CameraPosition);
            upperRight += (CameraPosition);
            upperLeft += (CameraPosition);



            //the idea here is to figure out the smallest square
            //(in tile space) that contains tiles
            //the offset is calculated before scaling
            float top = MathHelper.Min(
                MathHelper.Min(upperLeft.Y, lowerRight.Y),
                MathHelper.Min(upperRight.Y, lowerLeft.Y)) -
                WorldOffset.Y;

            float bottom = MathHelper.Max(
                MathHelper.Max(upperLeft.Y, lowerRight.Y),
                MathHelper.Max(upperRight.Y, lowerLeft.Y)) -
                WorldOffset.Y;
            float right = MathHelper.Max(
                MathHelper.Max(upperLeft.X, lowerRight.X),
                MathHelper.Max(upperRight.X, lowerLeft.X)) -
                WorldOffset.X;
            float left = MathHelper.Min(
                MathHelper.Min(upperLeft.X, lowerRight.X),
                MathHelper.Min(upperRight.X, lowerLeft.X)) -
                WorldOffset.X;


            //now figure out where we are in the tile sheet
            float scaledTileWidth = (float)_cellWidth * _scaleValue.X;
            float scaledTileHeight = (float)_cellHeight * _scaleValue.Y;

            //get the visible tiles
            _visibleTiles.X = (int)(left / (scaledTileWidth));
            _visibleTiles.Y = (int)(top / (scaledTileWidth));

            //get the number of visible tiles
            _visibleTiles.Height =
                (int)((bottom) / (scaledTileHeight)) - _visibleTiles.Y + 1;
            _visibleTiles.Width =
                (int)((right) / (scaledTileWidth)) - _visibleTiles.X + 1;

            //clamp the "upper left" values to 0
            if (_visibleTiles.X < 0)
            {
                _visibleTiles.X = 0;
            }

            if (_visibleTiles.X > (_width - 1))
            {
                _visibleTiles.X = _width;
            }

            if (_visibleTiles.Y < 0)
            {
                _visibleTiles.Y = 0;
            }

            if (_visibleTiles.Y > (_height - 1))
            {
                _visibleTiles.Y = _height;
            }


            //clamp the "lower right" values to the gameboard size
            if (_visibleTiles.Right > (_width - 1))
            {
                _visibleTiles.Width = (_width - _visibleTiles.X);
            }

            if (_visibleTiles.Right < 0)
            {
                _visibleTiles.Width = 0;
            }

            if (_visibleTiles.Bottom > (_height - 1))
            {
                _visibleTiles.Height = (_height - _visibleTiles.Y);
            }

            if (_visibleTiles.Bottom < 0)
            {
                _visibleTiles.Height = 0;
            }
        }

        protected override void DrawTiles(SpriteBatch batch)
        {
            float scaledTileWidth = (float)_cellWidth * _scaleValue.X;
            float scaledTileHeight = (float)_cellHeight * _scaleValue.Y;
            Vector2 screenCenter = new Vector2(
                (DisplaySize.X / 2),
                (DisplaySize.Y / 2));

            //begin a batch of sprites to be drawn all at once
            /*batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Deferred,
                SaveStateMode.None);*/

            //Rectangle sourceRect = new Rectangle();
            Vector2 scale = Vector2.One;
            Vector2 vecDummy;

            for (int x = _visibleTiles.Left; x < _visibleTiles.Right; x++)
            {
                for (int y = _visibleTiles.Top; y < _visibleTiles.Bottom; y++)
                {
                    if (_grid[x][y] != 0)
                    {
                        //Get the tile's position from the grid
                        //in this section we're using reference methods
                        //for the high frequency math functions
                        Vector2 position = Vector2.Zero;
                        position.X = (float)x * scaledTileWidth;
                        position.Y = (float)y * scaledTileHeight;


                        //offset the positions by the word position of the tile grid
                        //this is the actual position of the tile in world coordinates
                        vecDummy = WorldOffset;
                        Vector2.Add(ref position, ref vecDummy, out position);


                        //Now, we get the camera position relative to the tile's position
                        vecDummy = CameraPosition;
                        Vector2.Subtract(ref vecDummy, ref position,
                            out position);


                        //get the tile's final size (note that scaling is done after 
                        //determining the position)
                        Vector2.Multiply(ref _scaleValue, CameraZoom, out scale);

                        //get the source rectangle that defines the tile
                        //sheet.GetRectangle(ref grid[x][y],out sourceRect);

                        //Draw the tile.  Notice that position is used as the offset and
                        //the screen center is used as a position.  This is required to
                        //enable scaling and rotation about the center of the screen by
                        //drawing tiles as an offset from the center coordinate
                        /*batch.Draw(sheet.Texture, screenCenter, sourceRect, layerColor,
                            rotationValue, position, scale, SpriteEffects.None, 0.0f);*/
                        //Renderer2DComponent.AddSprite2D(sprite);
                    }
                }
            }
            //batch.End();
        }
    }
}

