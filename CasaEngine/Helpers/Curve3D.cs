
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


using Microsoft.Xna.Framework;

namespace XNAFinalEngine.Helpers
{

    public class Curve3D
    {


        private Curve _curveX = new();
        private Curve _curveY = new();
        private Curve _curveZ = new();



        public CurveLoopType PostLoop
        {
            get => _curveX.PostLoop;
            set
            {
                _curveX.PostLoop = value;
                _curveY.PostLoop = value;
                _curveZ.PostLoop = value;
            }
        } // PostLoop

        public int PointCount => _curveX.Keys.Count;

        public CurveLoopType PreLoop
        {
            get => _curveX.PostLoop;
            set
            {
                _curveX.PreLoop = value;
                _curveY.PreLoop = value;
                _curveZ.PreLoop = value;
            }
        } // PreLoop

        public float CurveTotalTime { get; private set; }

        public bool IsClose { get; private set; }



        public Curve3D()
        {
            IsClose = false;
            CurveTotalTime = 0;
        } // Curve3D



        public void AddPoint(Vector3 point, float time, Matrix worldMatrix)
        {
            point = Vector3.Transform(point, worldMatrix);
            _curveX.Keys.Add(new CurveKey(time, point.X));
            _curveY.Keys.Add(new CurveKey(time, point.Y));
            _curveZ.Keys.Add(new CurveKey(time, point.Z));
            CurveTotalTime = time;
        } // AddPoint

        public void AddPoint(Vector3 point, float time)
        {
            AddPoint(point, time, Matrix.Identity);
        } // AddPoint



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



        public Vector3 GetPoint(float time)
        {
            return new Vector3(_curveX.Evaluate(time), _curveY.Evaluate(time), _curveZ.Evaluate(time));
        } // GetPoint



        public void Reparametrize(float newTotalTime)
        {
            Curve newCurveX = new Curve();
            Curve newCurveY = new Curve();
            Curve newCurveZ = new Curve();
            float proportion = newTotalTime / CurveTotalTime;
            foreach (var key in _curveX.Keys)
            {
                newCurveX.Keys.Add(new CurveKey(key.Position * proportion, key.Value));
                newCurveY.Keys.Add(new CurveKey(key.Position * proportion, _curveY.Evaluate(key.Position)));
                newCurveZ.Keys.Add(new CurveKey(key.Position * proportion, _curveZ.Evaluate(key.Position)));
            }
            _curveX = newCurveX;
            _curveY = newCurveY;
            _curveZ = newCurveZ;
            CurveTotalTime = newTotalTime;
            BuildTangents();
        } // Reparametrize



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
                    {
                        prevIndex = i;
                    }
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
                    {
                        nextIndex = i;
                    }
                }
                // Build the x tangent
                prev = _curveX.Keys[prevIndex];
                next = _curveX.Keys[nextIndex];
                current = _curveX.Keys[i];
                SetCurveKeyTangent(ref prev, ref current, ref next, border);
                _curveX.Keys[i] = current;
                // Build the y tangent
                prev = _curveY.Keys[prevIndex];
                next = _curveY.Keys[nextIndex];
                current = _curveY.Keys[i];
                SetCurveKeyTangent(ref prev, ref current, ref next, border);
                _curveY.Keys[i] = current;
                // Build the z tangent
                prev = _curveZ.Keys[prevIndex];
                next = _curveZ.Keys[nextIndex];
                current = _curveZ.Keys[i];
                SetCurveKeyTangent(ref prev, ref current, ref next, border);
                _curveZ.Keys[i] = current;
            } // for
        } // BuildTangents

        private static void SetCurveKeyTangent(ref CurveKey prev, ref CurveKey cur, ref CurveKey next, bool border)
        {
            float dt = next.Position - prev.Position;
            float dv;

            if (border)
            {
                dv = prev.Value - next.Value;
            }
            else
            {
                dv = next.Value - prev.Value;
            }

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

        public static Curve3D Circle(float radius = 1, int numberOfPoints = 50)
        {
            return Circle(Matrix.Identity, radius, 50);
        } // Circle



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
            {
                poly.BuildTangents();
            }

            return poly;
        } // Polygon

        public static Curve3D Polygon(float sideLength = 50, int sides = 5, bool buildTangents = false)
        {
            return Polygon(Matrix.Identity, sideLength, sides, buildTangents);
        } // Polygon



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
            {
                hexagon.AddPoint(Vector3.Transform(coords[indexer], worldMatrix), indexer);
            }

            hexagon.Close();

            // Build tangents?
            if (buildTangents)
            {
                hexagon.BuildTangents();
            }

            return hexagon;
        } // Hexagon

        public static Curve3D Hexagon(float sideLength = 50, bool buildTangents = true)
        {
            return Hexagon(Matrix.Identity, sideLength, buildTangents);
        } // Hexagon


    } // Curve3D
} // XNAFinalEngine.Helpers