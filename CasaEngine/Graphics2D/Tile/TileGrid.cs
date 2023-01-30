//-----------------------------------------------------------------------------
// TileGrid.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------

using System;


using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;



namespace CasaEngine.Graphics2D.Tile
{
    /// <summary>
    /// EDUCATIONAL: Class used to align tiles to a regular grid.
    /// This represents a tiling "layer" in this sample
    /// </summary>
    public class TileGrid
        : TileLayer
    {

        private int[][] grid;
        //private SpriteSheet sheet;
        private int width;
        private int height;
        private int cellWidth;
        private int cellHeight;
        private Rectangle visibleTiles;

        private Vector2 scaleValue;



        public Vector2 TileScale
        {
            set
            {
                scaleValue = value;
                visibilityChanged = true;
            }
            get
            {
                return scaleValue;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="tileWidth"></param>
        /// <param name="tileHeight"></param>
        /// <param name="numXTiles"></param>
        /// <param name="numYTiles"></param>
        /// <param name="offset"></param>
        /// <param name="tileSheet"></param>
        /// <param name="graphicsComponent"></param>
        public TileGrid(int tileWidth, int tileHeight, int numXTiles, int numYTiles,
            Vector2 offset,  /*SpriteSheet tileSheet,*/
            GraphicsDeviceManager graphicsComponent/*, Renderer2DComponent Renderer2DComponent_*/)
            : base(offset, graphicsComponent/*, Renderer2DComponent_*/)
        {
            //sheet = tileSheet;
            width = numXTiles;
            height = numYTiles;
            cellWidth = tileWidth;
            cellHeight = tileHeight;

            scaleValue = Vector2.One;

            visibleTiles = new Rectangle(0, 0, width, height);

            grid = new int[width][];
            for (int i = 0; i < width; i++)
            {
                grid[i] = new int[height];
                for (int j = 0; j < height; j++)
                {
                    grid[i][j] = 0;
                }
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="xIndex"></param>
        /// <param name="yIndex"></param>
        /// <param name="tile"></param>
        public void SetTile(int xIndex, int yIndex, int tile)
        {
            grid[xIndex][yIndex] = tile;
        }


        /// <summary>
        /// This function determines which tiles are visible on the screen,
        /// given the current camera position, rotation, zoom, and tile scale
        /// </summary>
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
            float scaledTileWidth = (float)cellWidth * scaleValue.X;
            float scaledTileHeight = (float)cellHeight * scaleValue.Y;

            //get the visible tiles
            visibleTiles.X = (int)(left / (scaledTileWidth));
            visibleTiles.Y = (int)(top / (scaledTileWidth));

            //get the number of visible tiles
            visibleTiles.Height =
                (int)((bottom) / (scaledTileHeight)) - visibleTiles.Y + 1;
            visibleTiles.Width =
                (int)((right) / (scaledTileWidth)) - visibleTiles.X + 1;

            //clamp the "upper left" values to 0
            if (visibleTiles.X < 0) visibleTiles.X = 0;
            if (visibleTiles.X > (width - 1)) visibleTiles.X = width;
            if (visibleTiles.Y < 0) visibleTiles.Y = 0;
            if (visibleTiles.Y > (height - 1)) visibleTiles.Y = height;


            //clamp the "lower right" values to the gameboard size
            if (visibleTiles.Right > (width - 1))
                visibleTiles.Width = (width - visibleTiles.X);

            if (visibleTiles.Right < 0) visibleTiles.Width = 0;

            if (visibleTiles.Bottom > (height - 1))
                visibleTiles.Height = (height - visibleTiles.Y);

            if (visibleTiles.Bottom < 0) visibleTiles.Height = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="batch"></param>
        protected override void DrawTiles(SpriteBatch batch)
        {
            float scaledTileWidth = (float)cellWidth * scaleValue.X;
            float scaledTileHeight = (float)cellHeight * scaleValue.Y;
            Vector2 screenCenter = new Vector2(
                (DisplaySize.X / 2),
                (DisplaySize.Y / 2));

            //begin a batch of sprites to be drawn all at once
            /*batch.Begin(SpriteBlendMode.AlphaBlend, SpriteSortMode.Deferred,
                SaveStateMode.None);*/

            //Rectangle sourceRect = new Rectangle();
            Vector2 scale = Vector2.One;
            Vector2 vecDummy;

            for (int x = visibleTiles.Left; x < visibleTiles.Right; x++)
            {
                for (int y = visibleTiles.Top; y < visibleTiles.Bottom; y++)
                {
                    if (grid[x][y] != 0)
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
                        Vector2.Multiply(ref scaleValue, CameraZoom, out scale);

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

