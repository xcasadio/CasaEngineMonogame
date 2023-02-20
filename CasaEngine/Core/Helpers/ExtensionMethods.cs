
/*
Copyright (c) 2008-2012, Laboratorio de Investigación y Desarrollo en Visualización y Computación Gráfica - 
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
Authors: Digital Jellyfish Design Ltd (http://forums.create.msdn.com/forums/p/16395/132030.aspx)
         deadlydog (http://www.danskingdom.com)
         Schneider, José
-----------------------------------------------------------------------------------------------------------------------------------------------

String extension methods based on the class StringHelper.cs from RacingGame. License: Microsoft_Permissive_License
  
*/

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;

namespace CasaEngine.Core.Helpers
{
    public static class ExtensionMethods
    {
        private static readonly char[] Digits = new[] { '9', '8', '7', '6', '5', '4', '3', '2', '1', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };

        public static StringBuilder ClearXbox(this StringBuilder stringBuilder)
        {
#if XBOX
                if (stringBuilder.Length > 0)
                    stringBuilder.Length = 0;
                    //Text.IsRemoved(0, Text.Length - 1); // Clear is not supported in XBOX.
#else
            stringBuilder.Clear();
#endif
            return stringBuilder;
        }

        public static StringBuilder AppendWithoutGarbage(this StringBuilder stringBuilder, int number, bool insertDots = false)
        {
            if (number < 0)
            {
                stringBuilder.Append('-');
            }

            var i = 0;
            var index = stringBuilder.Length;
            do
            {
                if (insertDots && i == 3)
                {
                    stringBuilder.Insert(index, ".");
                    i = 0;
                }
                // StringBuilder.Insert(Int32, Char) calls ToString() internally
                // http://www.gavpugh.com/2010/04/01/xnac-avoiding-garbage-when-working-with-stringbuilder/
                stringBuilder.Insert(index, Digits, number % 10 + 9, 1);
                number /= 10;
                i++;
            }
            while (number != 0);

            return stringBuilder;
        } // AppendWithoutGarbage

        public static bool IsNumericFloat(this string str)
        {
            return str.IsNumericFloat(CultureInfo.InvariantCulture.NumberFormat);
        } // IsNumericFloat

        public static bool IsNumericFloat(this string str, NumberFormatInfo numberFormat)
        {
            // Can't be a float if string is not valid!
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }

            //not supported by Convert.ToSingle:
            //if (str.EndsWith("f"))
            //	str = str.Substring(0, str.Length - 1);

            // Only 1 decimal point is allowed
            if (AllowOnlyOneDecimalPoint(str, numberFormat) == false)
            {
                return false;
            }

            // + allows in the first,last,don't allow in middle of the string
            // - allows in the first,last,don't allow in middle of the string
            // $ allows in the first,last,don't allow in middle of the string
            // , allows in the last,middle,don't allow in first char of the string
            // . allows in the first,last,middle, allows in all the indexs
            var retVal = false;

            // If string is just 1 letter, don't allow it to be a sign
            if (str.Length == 1 &&
                "+-$.,".IndexOf(str[0]) >= 0)
            {
                return false;
            }

            for (var i = 0; i < str.Length; i++)
            {
                // For first indexchar
                var pChar =
                    //char.Parse(str.Substring(i, 1));
                    Convert.ToChar(str.Substring(i, 1));

                if (retVal)
                {
                    retVal = false;
                }

                if (str.IndexOf(pChar) == 0)
                {
                    retVal = "+-$.0123456789".IndexOf(pChar) >= 0 ? true : false;
                }
                // For middle characters
                if (!retVal && str.IndexOf(pChar) > 0 &&
                    str.IndexOf(pChar) < str.Length - 1)
                {
                    retVal = ",.0123456789".IndexOf(pChar) >= 0 ? true : false;
                }
                // For last characters
                if (!retVal && str.IndexOf(pChar) == str.Length - 1)
                {
                    retVal = "+-$,.0123456789".IndexOf(pChar) >= 0 ? true : false;
                }

                if (!retVal)
                {
                    break;
                }
            }

            return retVal;
        } // IsNumericFloat

        public static bool IsNumericInt(this string str)
        {
            // Can't be an int if string is not valid!
            if (string.IsNullOrEmpty(str))
            {
                return false;
            }

            // Go through every letter in str
            var strPos = 0;
            foreach (var ch in str)
            {
                // Only 0-9 are allowed
                if ("0123456789".IndexOf(ch) < 0 &&
                    // Allow +/- for first char
                    (strPos > 0 || ch != '-' && ch != '+'))
                {
                    return false;
                }

                strPos++;
            } // foreach (ch in str)

            // All fine, return true, this is a number!
            return true;
        } // IsNumericInt

        private static bool AllowOnlyOneDecimalPoint(string str, NumberFormatInfo numberFormat)
        {
            var strInChars = str.ToCharArray();
            var hasGroupSeperator = false;
            var decimalSeperatorCount = 0;
            for (var i = 0; i < strInChars.Length; i++)
            {
                if (numberFormat.CurrencyDecimalSeparator.IndexOf(strInChars[i]) == 0)
                {
                    decimalSeperatorCount++;
                } // if (numberFormat.CurrencyDecimalSeparator.IndexOf)

                // has float group seperators  ?
                if (numberFormat.CurrencyGroupSeparator.IndexOf(strInChars[i]) == 0)
                {
                    hasGroupSeperator = true;
                } // if (numberFormat.CurrencyGroupSeparator.IndexOf)
            } // for (int)

            if (hasGroupSeperator)
            {
                // If first digit is the group seperator or begins with 0,
                // there is something wrong, the group seperator is used as a comma.
                if (str.StartsWith(numberFormat.CurrencyGroupSeparator) ||
                    strInChars[0] == '0')
                {
                    return false;
                }

                // look only at the digits in front of the decimal point
                var splittedByDecimalSeperator = str.Split(
                    numberFormat.CurrencyDecimalSeparator.ToCharArray());

                //   ==> 1.000 -> 000.1  ==> only after 3 digits 
                var firstSplittedInChars = splittedByDecimalSeperator[0].ToCharArray();
                var arrayLength = firstSplittedInChars.Length;
                var firstSplittedInCharsInverted = new char[arrayLength];
                for (var i = 0; i < arrayLength; i++)
                {
                    firstSplittedInCharsInverted[i] =
                        firstSplittedInChars[arrayLength - 1 - i];
                } // for (int)

                // group seperators are only allowed between 3 digits -> 1.000.000
                for (var i = 0; i < arrayLength; i++)
                {
                    if (i % 3 != 0 && numberFormat.CurrencyGroupSeparator.IndexOf(
                        firstSplittedInCharsInverted[i]) == 0)
                    {
                        return false;
                    } // if (i)
                } // for (int)
            } // if (hasGroupSeperator)
            if (decimalSeperatorCount > 1)
            {
                return false;
            }

            return true;
        } // AllowOnlyOneDecimalPoint

        public static string PlusOne(this string name)
        {
            var regex = new Regex(@"(\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var match = regex.Match(name);

            if (match.Success)
            {
                var numberPlusOne = (int)double.Parse(match.Value) + 1;
                return regex.Replace(name, numberPlusOne.ToString());
            }
            return name + "1";
        } // PlusOne

        public static float ArcTanAngle(float x, float y)
        {
            if (x == 0)
            {
                if (y == 1)
                {
                    return MathHelper.PiOver2;
                }

                return -MathHelper.PiOver2;
            }
            if (x > 0)
            {
                return (float)Math.Atan(y / x);
            }

            if (x < 0)
            {
                if (y > 0)
                {
                    return (float)Math.Atan(y / x) + MathHelper.Pi;
                }

                return (float)Math.Atan(y / x) - MathHelper.Pi;
            }
            return 0;
        } // ArcTanAngle

        public static Vector3 AngleTo(Vector3 from, Vector3 location)
        {
            var angle = new Vector3();
            var v3 = Vector3.Normalize(location - from);
            angle.X = (float)Math.Asin(v3.Y);
            angle.Y = ArcTanAngle(-v3.Z, -v3.X);
            return angle;
        } // AngleTo

        public static Vector3 GetYawPitchRoll(this Quaternion quaternion)
        {
            var rotationaxes = new Vector3();

            var forward = Vector3.Transform(Vector3.Forward, quaternion);
            var up = Vector3.Transform(Vector3.Up, quaternion);
            rotationaxes = AngleTo(new Vector3(), forward);
            if (rotationaxes.X == MathHelper.PiOver2)
            {
                rotationaxes.Y = ArcTanAngle(up.Z, up.X);
                rotationaxes.Z = 0;
            }
            else if (rotationaxes.X == -MathHelper.PiOver2)
            {
                rotationaxes.Y = ArcTanAngle(-up.Z, -up.X);
                rotationaxes.Z = 0;
            }
            else
            {
                up = Vector3.Transform(up, Matrix.CreateRotationY(-rotationaxes.Y));
                up = Vector3.Transform(up, Matrix.CreateRotationX(-rotationaxes.X));
                rotationaxes.Z = ArcTanAngle(up.Y, -up.X);
            }

            // Special cases.
            if (rotationaxes.Y <= (float)-Math.PI)
            {
                rotationaxes.Y = (float)Math.PI;
            }

            if (rotationaxes.Z <= (float)-Math.PI)
            {
                rotationaxes.Z = (float)Math.PI;
            }

            if (rotationaxes.Y >= Math.PI && rotationaxes.Z >= Math.PI)
            {
                rotationaxes.Y = 0;
                rotationaxes.Z = 0;
                rotationaxes.X = (float)Math.PI - rotationaxes.X;
            }

            return new Vector3(rotationaxes.Y, rotationaxes.X, rotationaxes.Z);
        } // GetYawPitchRoll

    } // ExtensionMethods
} // XNAFinalEngineBase.Helpers