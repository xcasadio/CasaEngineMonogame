
/*
Copyright (c) 2008-2011, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
                         Departamento de Ciencias e Ingeniería de la Computación - Universidad Nacional del Sur.
All rights reserved.
Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

•	Redistributions of source code must retain the above copyright, this list of conditions and the following disclaimer.

•	Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer
    in the documentation and/or other materials provided with the distribution.

•	Neither the name of the Universidad Nacional del Sur nor the names of its contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS ''AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

-----------------------------------------------------------------------------------------------------------------------------------------------
Authors: Schneider, José Ignacio (jis@cs.uns.edu.ar)
         Berwanger, Renê (polygon and hexagon)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/

using System;


using Microsoft.Xna.Framework;

namespace XNAFinalEngine.Helpers
{

    /// <summary>
    /// Curve 3D. Assumes that the firts point it's allways 0.
    /// 
    /// To create a curve:
    /// 
    ///  // Create instance
    ///  * curve = new Curve3D();
    ///  
    ///  // Add points
    ///  * curve.AddPoint(..., 0);
    ///  * curve.AddPoint(...);
    ///  ...
    ///  
    ///  // Finish the curve calculating its tangents.
    ///  * curve.SetTangents();
    /// </summary>
    public class Curve3D
    {


        private Curve curveX = new Curve();
        private Curve curveY = new Curve();
        private Curve curveZ = new Curve();



        /// <summary>
        /// The PreLoop and PostLoop types determine how the curve will interpret positions before the first key or after the last key.
        /// </summary>
        public CurveLoopType PostLoop
        {
            get { return curveX.PostLoop; }
            set
            {
                curveX.PostLoop = value;
                curveY.PostLoop = value;
                curveZ.PostLoop = value;
            }
        } // PostLoop

        /// <summary>
        /// Point count.
        /// </summary>
        public int PointCount { get { return curveX.Keys.Count; } }

        /// <summary>
        /// The PreLoop and PostLoop types determine how the curve will interpret positions before the first key or after the last key.
        /// </summary>
        public CurveLoopType PreLoop
        {
            get { return curveX.PostLoop; }
            set
            {
                curveX.PreLoop = value;
                curveY.PreLoop = value;
                curveZ.PreLoop = value;
            }
        } // PreLoop

        /// <summary>
        /// Stores the last point's time.
        /// </summary>
        public float CurveTotalTime { get; private set; }

        /// <summary>
        /// is the curve close?
        /// </summary>
        public bool IsClose { get; private set; }



        /// <summary>
        /// Curve 3D. Assumes that the firts point it's allways 0.
        /// </summary>
        public Curve3D()
        {
            IsClose = false;
            CurveTotalTime = 0;
        } // Curve3D



        /// <summary>
        /// Add a point to the curve. Assumes that the firts point it's allways 0.
        /// The worldMatrix modify the point's position to the world matrix coordinate system.
        /// </summary>
        public void AddPoint(Vector3 point, float time, Matrix worldMatrix)
        {
            point = Vector3.Transform(point, worldMatrix);
            curveX.Keys.Add(new CurveKey(time, point.X));
            curveY.Keys.Add(new CurveKey(time, point.Y));
            curveZ.Keys.Add(new CurveKey(time, point.Z));
            CurveTotalTime = time;
        } // AddPoint

        /// <summary>
        /// Add a point to the curve. Assumes that the firts point it's allways 0.
        /// </summary>
        public void AddPoint(Vector3 point, float time)
        {
            AddPoint(point, time, Matrix.Identity);
        } // AddPoint



        /// <summary>
        /// Close the curve
        /// </summary>
        public void Close()
        {
            if (!IsClose)
            {
                float newTime = (CurveTotalTime / (PointCount - 1)) * PointCount;
                AddPoint(GetPoint(0), newTime);
                CurveTotalTime = newTime;
                IsClose = true;
            }
        } // Close



        /// <summary>
        /// Get point
        /// </summary>
        public Vector3 GetPoint(float time)
        {
            return new Vector3(curveX.Evaluate(time), curveY.Evaluate(time), curveZ.Evaluate(time));
        } // GetPoint



        /// <summary>
        /// Reparametrize a curve.
        /// </summary>
        public void Reparametrize(float newTotalTime)
        {
            Curve newCurveX = new Curve();
            Curve newCurveY = new Curve();
            Curve newCurveZ = new Curve();
            float proportion = newTotalTime / CurveTotalTime;
            foreach (var key in curveX.Keys)
            {
                newCurveX.Keys.Add(new CurveKey(key.Position * proportion, key.Value));
                newCurveY.Keys.Add(new CurveKey(key.Position * proportion, curveY.Evaluate(key.Position)));
                newCurveZ.Keys.Add(new CurveKey(key.Position * proportion, curveZ.Evaluate(key.Position)));
            }
            curveX = newCurveX;
            curveY = newCurveY;
            curveZ = newCurveZ;
            CurveTotalTime = newTotalTime;
            BuildTangents();
        } // Reparametrize



        /// <summary>
        /// Build tangents. The tangents of the CurveKeys control the shape of the Curve.
        /// Setting the tangents of the CurveKeys to the slope between the previous and next CurveKey will give a curve that moves smoothly through each point on the curve.
        /// </summary>
        public void BuildTangents()
        {
            CurveKey prev;
            CurveKey current;
            CurveKey next;
            int prevIndex;
            int nextIndex;
            bool border; // If it's the border then the tangent calculations change.

            for (int i = 0; i < PointCount; i++)
            {
                border = false;
                prevIndex = i - 1;
                if (prevIndex < 0) // If it's the first curve key
                {
                    if (IsClose)
                    {
                        prevIndex = PointCount - 2;
                        border = true;
                    }
                    else
                        prevIndex = i;
                }

                nextIndex = i + 1;
                if (nextIndex == PointCount) // If it's the last curve key
                {
                    if (IsClose)
                    {
                        nextIndex = 1;
                        border = true;
                    }
                    else
                        nextIndex = i;
                }
                // Build the x tangent
                prev = curveX.Keys[prevIndex];
                next = curveX.Keys[nextIndex];
                current = curveX.Keys[i];
                SetCurveKeyTangent(ref prev, ref current, ref next, border);
                curveX.Keys[i] = current;
                // Build the y tangent
                prev = curveY.Keys[prevIndex];
                next = curveY.Keys[nextIndex];
                current = curveY.Keys[i];
                SetCurveKeyTangent(ref prev, ref current, ref next, border);
                curveY.Keys[i] = current;
                // Build the z tangent
                prev = curveZ.Keys[prevIndex];
                next = curveZ.Keys[nextIndex];
                current = curveZ.Keys[i];
                SetCurveKeyTangent(ref prev, ref current, ref next, border);
                curveZ.Keys[i] = current;
            } // for
        } // BuildTangents

        /// <summary>
        /// Set curve key tangent
        /// </summary>
        private static void SetCurveKeyTangent(ref CurveKey prev, ref CurveKey cur, ref CurveKey next, bool border)
        {
            float dt = next.Position - prev.Position;
            float dv;

            if (border)
                dv = prev.Value - next.Value;
            else
                dv = next.Value - prev.Value;

            if (Math.Abs(dv) < float.Epsilon)
            {
                cur.TangentIn = 0;
                cur.TangentOut = 0;
            }
            else
            {
                // The in and out tangents should be equal to the  slope between the adjacent keys.
                cur.TangentIn = dv * (cur.Position - prev.Position) / dt;
                cur.TangentOut = dv * (next.Position - cur.Position) / dt;
            }
        } // SetCurveKeyTangent



        /// <summary>
        /// Circle
        /// The worldMatrix modify the point's position to the world matrix coordinate system.
        /// </summary>
        public static Curve3D Circle(Matrix worldMatrix, float radius = 1, int numberOfPoints = 50)
        {
            Curve3D circle = new Curve3D();
            for (float i = 0; i < 3.1416f * 2; i = i + (3.1416f / numberOfPoints))
            {
                float x = (float)Math.Sin(i) * radius;
                float y = (float)Math.Cos(i) * radius;
                circle.AddPoint(Vector3.Transform(new Vector3(x, y, 0), worldMatrix), i);
            }
            circle.Close();
            circle.BuildTangents();
            return circle;
        } // Circle

        /// <summary>
        /// Circle
        /// </summary>
        public static Curve3D Circle(float radius = 1, int numberOfPoints = 50)
        {
            return Circle(Matrix.Identity, radius, 50);
        } // Circle



        /// <summary>
        /// Polygon.
        /// The worldMatrix modify the point's position to the world matrix coordinate system.
        /// </summary>
        public static Curve3D Polygon(Matrix worldMatrix, float sideLenght, int sides, bool buildTangents)
        {
            Curve3D poly = new Curve3D();
            float rotationAngle = (((((sides * 180) - 360) / sides) / 2) + 180);

            for (int indexer = 0; indexer < sides; indexer++)
            {
                Vector3 coord = new Vector3((sideLenght * (float)Math.Cos(indexer * 2 * Math.PI / sides)), (sideLenght * (float)Math.Sin(indexer * 2 * Math.PI / sides)), 0);

                if (sides % 2 == 0) // even
                {
                    poly.AddPoint(Vector3.Transform(coord, worldMatrix), indexer);
                }
                else // odd
                {
                    poly.AddPoint(Vector3.Transform(coord, worldMatrix * Matrix.CreateRotationZ(MathHelper.ToRadians(rotationAngle))), indexer);
                }
            }

            poly.Close();
            // Build tangents?
            if (buildTangents)
                poly.BuildTangents();

            return poly;
        } // Polygon

        /// <summary>
        /// Polygon.
        /// </summary>
        public static Curve3D Polygon(float sideLength = 50, int sides = 5, bool buildTangents = false)
        {
            return Polygon(Matrix.Identity, sideLength, sides, buildTangents);
        } // Polygon



        /// <summary>
        /// Hexagon.
        /// The worldMatrix modify the point's position to the world matrix coordinate system.
        /// </summary>
        public static Curve3D Hexagon(Matrix worldMatrix, float sideLength = 50, bool buildTangents = true)
        {
            Curve3D hexagon = new Curve3D();
            // Math things
            float sl = sideLength;
            float hs = sl / 2;
            float ls = (float)Math.Sin((double)MathHelper.ToRadians(60)) * sl;
            float h = (float)Math.Sin((double)MathHelper.ToRadians(15)) * hs;

            // Coords generation
            Vector3[] coords = new Vector3[6];
            coords[0] = new Vector3(0, ls, 0);
            coords[1] = new Vector3(hs, 0, 0);
            coords[2] = new Vector3(hs + sl, 0, 0);
            coords[3] = new Vector3(2 * sl, ls, 0);
            coords[4] = new Vector3(hs + sl, 2 * ls, 0);
            coords[5] = new Vector3(hs, 2 * ls, 0);

            // Center in world matrix
            worldMatrix = worldMatrix * Matrix.CreateTranslation(new Vector3(-sideLength, -(sideLength - h), 0));

            // add Points
            for (int indexer = 0; indexer < coords.Length; indexer++)
                hexagon.AddPoint(Vector3.Transform(coords[indexer], worldMatrix), indexer);

            hexagon.Close();

            // Build tangents?
            if (buildTangents)
                hexagon.BuildTangents();

            return hexagon;
        } // Hexagon

        /// <summary>
        /// Hexagon.
        /// </summary>
        public static Curve3D Hexagon(float sideLength = 50, bool buildTangents = true)
        {
            return Hexagon(Matrix.Identity, sideLength, buildTangents);
        } // Hexagon


    } // Curve3D
} // XNAFinalEngine.Helpers