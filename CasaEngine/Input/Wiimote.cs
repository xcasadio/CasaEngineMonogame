
#region License
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
Author: Schneider, José Ignacio (jis@cs.uns.edu.ar)
-----------------------------------------------------------------------------------------------------------------------------------------------

*/
#endregion

#region Using directives
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using WiimoteLib;
using XNAFinalEngine.Assets;
using XNAFinalEngine.EngineCore;
using XNAFinalEngine.Helpers;
using Screen = XNAFinalEngine.EngineCore.Screen;
#endregion

namespace XNAFinalEngine.Input
{

	/// <summary>
	/// Wiimote and addons.
    /// It utilizes the Wiimotelib library. http://wiimotelib.codeplex.com/ and http://www.brianpeek.com/forums
    /// The Wiimote that aren't connected will be ignored.
	/// </summary>
    /// <remarks>
    /// Wiimote support was deprecated but probably still works.
    /// </remarks>
	public class Wiimote
    {

        #region Variables

        // The object of the class wiimoteLib that allow us to connect with the physical Wiimote.
        private WiimoteLib.Wiimote wiimote;

        // The id number of the wiimote.
        private readonly int playerIndex;
        
        // Is the wiimote connected?
        private bool isConnected;
        
        // Wiimote state, set every frame in the Update method.
		private WiimoteState wiimoteState, wiimoteStateLastFrame;
        
        // Chronometer used for the vibrations.
        private readonly Chronometer chronometer = new Chronometer();
        
        // Duration of the current vibration.
        private double duration;
        
        // Last position of the wiimote pointer.
        private PointF lastPosition;

		#endregion

        #region Properties

        /// <summary>
        /// The object of the class wiimoteLib that allow us to connect with the physical Wiimote.
        /// </summary>
        public WiimoteLib.Wiimote LowLevelWiimote { get { return wiimote; } }

        /// <summary>
        /// Is the wiimote connected?
        /// </summary>
        public bool IsConnected { get { return isConnected; } }

        /// <summary>
        /// What type of extension is connected to the wiimote?
        /// </summary>
        public ExtensionType ExtensionType { get { return wiimote.WiimoteState.ExtensionType; } }

        #region Buttons (A, B, 1, 2, Minus, Plus, Home)

        /// <summary>
        /// Wiimote A button pressed
        /// </summary>
        public bool APressed { get { return wiimoteState.ButtonState.A; } }

        /// <summary>
        /// Wiimote A button just pressed
        /// </summary>
        public bool AJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.A && !(wiimoteStateLastFrame.ButtonState.A);
            }
        } // AJustPressed

        /// <summary>
        /// Wiimote B button pressed
        /// </summary>
        public bool BPressed { get { return wiimoteState.ButtonState.B; } }

        /// <summary>
        /// Wiimote B button just pressed
        /// </summary>
        public bool BJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.B && !(wiimoteStateLastFrame.ButtonState.B);
            }
        } // BJustPressed

        /// <summary>
        /// Wiimote 1 button pressed
        /// </summary>
        public bool OnePressed { get { return wiimoteState.ButtonState.One; } }

        /// <summary>
        /// Wiimote 1 button just pressed
        /// </summary>
        public bool OneJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.One && !(wiimoteStateLastFrame.ButtonState.One);
            }
        } // OneJustPressed

        /// <summary>
        /// Wiimote 2 button pressed
        /// </summary>
        public bool TwoPressed { get { return wiimoteState.ButtonState.Two; } }

        /// <summary>
        /// Wiimote 2 button just pressed
        /// </summary>
        public bool TwoJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Two && !(wiimoteStateLastFrame.ButtonState.Two);
            }
        } // TwoJustPressed

        /// <summary>
        /// Wiimote minus button pressed
        /// </summary>
        public bool MinusPressed { get { return wiimote.WiimoteState.ButtonState.Minus; } }

        /// <summary>
        /// Wiimote minus button just pressed
        /// </summary>
        public bool MinusJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Minus && !(wiimoteStateLastFrame.ButtonState.Minus);
            }
        } // MinusJustPressed

        /// <summary>
        /// Wiimote plus button pressed
        /// </summary>
        public bool PlusPressed { get { return wiimote.WiimoteState.ButtonState.Plus; } }

        /// <summary>
        /// Wiimote plus button just pressed
        /// </summary>
        public bool PlusJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Plus && !(wiimoteStateLastFrame.ButtonState.Plus);
            }
        } // PlusJustPressed

        /// <summary>
        /// Wiimote home button pressed
        /// </summary>
        public bool HomePressed { get { return wiimote.WiimoteState.ButtonState.Home; } }

        /// <summary>
        /// Wiimote home button just pressed
        /// </summary>
        public bool HomeJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Home && !(wiimoteStateLastFrame.ButtonState.Home);
            }
        } // HomeJustPressed
        
        #endregion

        #region DPad

        /// <summary>
        /// Wiimote DPad left button pressed
        /// </summary>
        public bool DPadLeftPressed { get { return wiimoteState.ButtonState.Left; } }

        /// <summary>
        /// Wiimote DPad left button just pressed
        /// </summary>
        public bool DPadLeftJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Left && !(wiimoteStateLastFrame.ButtonState.Left);
            }
        } // DPadLeftJustPressed

        /// <summary>
        /// Wiimote DPad right button pressed
        /// </summary>
        public bool DPadRightPressed { get { return wiimoteState.ButtonState.Right; } }

        /// <summary>
        /// Wiimote DPad right button just pressed
        /// </summary>
        public bool DPadRightJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Right && !(wiimoteStateLastFrame.ButtonState.Right);
            }
        } // DPadRightJustPressed

        /// <summary>
        /// Wiimote DPad up button pressed
        /// </summary>
        public bool DPadUpPressed { get { return wiimoteState.ButtonState.Up; } }

        /// <summary>
        /// Wiimote DPad up button just pressed
        /// </summary>
        public bool DPadUpJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Up && !(wiimoteStateLastFrame.ButtonState.Up);
            }
        } // DPadUpJustPressed

        /// <summary>
        /// Wiimote DPad down button button pressed
        /// </summary>
        public bool DPadDownPressed { get { return wiimoteState.ButtonState.Down; } }

        /// <summary>
        /// Wiimote DPad down button just pressed
        /// </summary>
        public bool DPadDownJustPressed
        {
            get
            {
                return wiimoteState.ButtonState.Down && !(wiimoteStateLastFrame.ButtonState.Down);
            }
        } // DPadDownJustPressed

        #endregion

        #region Accelerometers

        /// <summary>
        /// Wiimote X accelerometer (in g force scale).
        /// Values between 0 and 1 are rotations, values greater than that are translations.
        /// The accelerometer measures with a minimum full-scale range of ±3 g.
        /// </summary>
        public float XAccelerometer { get { return wiimoteState.AccelState.Values.X; } }

        /// <summary>
        /// Wiimote Y accelerometer (in g force scale).
        /// Values between 0 and 1 are rotations, values greater than that are translations.
        /// The accelerometer measures with a minimum full-scale range of ±3 g.
        /// </summary>
        public float YAccelerometer { get { return wiimoteState.AccelState.Values.Y; } }

        /// <summary>
        /// Wiimote Z accelerometer (in g force scale).
        /// Values between 0 and 1 are rotations, values greater than that are translations.
        /// The accelerometer measures with a minimum full-scale range of ±3 g.
        /// </summary>
        public float ZAccelerometer { get { return wiimoteState.AccelState.Values.Z; } }
        
        #endregion

        #region Nunchuk

        #region Buttons

        /// <summary>
        /// Nunchuk C button pressed
        /// </summary>
        public bool NunchukCPressed { get { return wiimoteState.NunchukState.C; } }

        /// <summary>
        /// Nunchuk C button just pressed
        /// </summary>
        public bool NunchukCJustPressed
        {
            get
            {
                return wiimoteState.NunchukState.C && !(wiimoteStateLastFrame.NunchukState.C);
            }
        } // NunchukCJustPressed

        /// <summary>
        /// Nunchuk Z button pressed
        /// </summary>
        public bool NunchukZPressed { get { return wiimoteState.NunchukState.Z; } }

        /// <summary>
        /// Nunchuk Z button just pressed
        /// </summary>
        public bool NunchukZJustPressed
        {
            get
            {
                return wiimoteState.NunchukState.Z && !(wiimoteStateLastFrame.NunchukState.Z);
            }
        } // NunchukZJustPressed

        #endregion

        #region Stick

        /// <summary>
        /// Nunchuk thumb stick X movement
        /// </summary>
        public float NunchukStickXMovement { get { return wiimoteState.NunchukState.Joystick.X; } }

        /// <summary>
        /// Nunchuk thumb stick Y movement
        /// </summary>
        public float NunchukStickYMovement { get { return wiimoteState.NunchukState.Joystick.Y; } }

        #endregion

        #region Accelerometers

        /// <summary>
        /// Nunchuk X accelerometer (in g force scale).
        /// Values between 0 and 1 are rotations, values greater than that are translations.
        /// The accelerometer measures with a minimum full-scale range of ±3 g.
        /// </summary>
        public float NunchukXAccelerometer { get { return wiimoteState.NunchukState.AccelState.Values.X; } }

        /// <summary>
        /// Nunchuk Y accelerometer (in g force scale).
        /// Values between 0 and 1 are rotations, values greater than that are translations.
        /// The accelerometer measures with a minimum full-scale range of ±3 g.
        /// </summary>
        public float NunchukYAccelerometer { get { return wiimoteState.NunchukState.AccelState.Values.Y; } }

        /// <summary>
        /// Nunchuk Z accelerometer (in g force scale).
        /// Values between 0 and 1 are rotations, values greater than that are translations.
        /// The accelerometer measures with a minimum full-scale range of ±3 g.
        /// </summary>
        public float NunchukZAccelerometer { get { return wiimoteState.NunchukState.AccelState.Values.Z; } }

        #endregion

        #endregion

        #region IR

        /// <summary>
        /// Wiimote IR Sensor 0 position. La posicion se encuentra definida en un intervalo entre 0 y 1.
        /// </summary>
        public PointF WiimoteIRSensor0Position { get { return wiimoteState.IRState.IRSensors[0].Position; } }

        /// <summary>
        /// Wiimote IR Sensor 1 position. La posicion se encuentra definida en un intervalo entre 0 y 1.
        /// </summary>
        public PointF WiimoteIRSensor1Position { get { return wiimoteState.IRState.IRSensors[1].Position; } }

        /// <summary>
        /// Wiimote IR Sensor 2 position. La posicion se encuentra definida en un intervalo entre 0 y 1.
        /// </summary>
        public PointF WiimoteIRSensor2Position { get { return wiimoteState.IRState.IRSensors[2].Position; } }

        /// <summary>
        /// Wiimote IR Sensor 3 position. La posicion se encuentra definida en un intervalo entre 0 y 1.
        /// </summary>
        public PointF WiimoteIRSensor3Position { get { return wiimoteState.IRState.IRSensors[3].Position; } }

        /// <summary>
        /// Is Wiimote IR Sensor 0 seen.
        /// </summary>
        public bool WiimoteIRSensor0Found { get { return wiimoteState.IRState.IRSensors[0].Found; } }

        /// <summary>
        /// Is Wiimote IR Sensor 1 seen.
        /// </summary>
        public bool WiimoteIRSensor1Found { get { return wiimoteState.IRState.IRSensors[1].Found; } }

        /// <summary>
        /// Is Wiimote IR Sensor 2 seen.
        /// </summary>
        public bool WiimoteIRSensor2Found { get { return wiimoteState.IRState.IRSensors[2].Found; } }

        /// <summary>
        /// Is Wiimote IR Sensor 3 seen.
        /// </summary>
        public bool WiimoteIRSensor3Found { get { return wiimoteState.IRState.IRSensors[3].Found; } }

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// Creates the basic structures, but this constructor doesn’t raise the actual wiimote. 
        /// This works in that way because assumes that the inexistent Wiimotes are up with default values.
        /// </summary>
        private Wiimote(int _playerIndex)
        {
            wiimote = new WiimoteLib.Wiimote();
            playerIndex = _playerIndex;
            isConnected = false;
            wiimoteState = new WiimoteState();
        } // Wiimote

        /// <summary>
        /// Assign the wiimoteLib object to the correct Wiimote object.
        /// </summary>
        private void AssignWiimote(WiimoteLib.Wiimote _wiimote)
        {
            wiimote = _wiimote;
            // Init the IR camera
            if (wiimote.WiimoteState.ExtensionType != ExtensionType.BalanceBoard)
            {
                wiimote.SetReportType(InputReport.IRExtensionAccel, IRSensitivity.Maximum, true);
            }
            // Display the player number using the wiimote's lights
            wiimote.SetLEDs(playerIndex);
            isConnected = true;
        } // AssignWiimote

        #endregion

        #region Update

        /// <summary>
        /// Update Wiimote State
        /// </summary>
        public void Update()
        {
            CloneState(ref wiimoteStateLastFrame, wiimoteState);
            CloneState(ref wiimoteState, wiimote.WiimoteState);
            if (duration != 0) // If the vibration is active
            {
                if (chronometer.ElapsedTime > duration) // If the vibration is over
                {
                    SetVibration(false);
                }
            }
        } // Update

        /// <summary>
        /// Clones the Wiimote state. A simple assignation doesn’t work.
        /// </summary>
        public void CloneState(ref WiimoteState wiimoteStateDestination, WiimoteState wiimoteStateOrigin)
        {
            // Buttons //
            wiimoteStateDestination = new WiimoteState
            {
                ButtonState =
                    {
                        A     = wiimoteStateOrigin.ButtonState.A,
                        B     = wiimoteStateOrigin.ButtonState.B,
                        One   = wiimoteStateOrigin.ButtonState.One,
                        Two   = wiimoteStateOrigin.ButtonState.Two,
                        Minus = wiimoteStateOrigin.ButtonState.Minus,
                        Plus  = wiimoteStateOrigin.ButtonState.Plus,
                        Home  = wiimoteStateOrigin.ButtonState.Home,
                        Left  = wiimoteStateOrigin.ButtonState.Left,
                        Right = wiimoteStateOrigin.ButtonState.Right,
                        Up    = wiimoteStateOrigin.ButtonState.Up,
                        Down  = wiimoteStateOrigin.ButtonState.Down
                    }
            };
            // DPad //
            // Accelerometers //
            wiimoteStateDestination.AccelState.Values.X = wiimoteStateOrigin.AccelState.Values.X;
            wiimoteStateDestination.AccelState.Values.Y = wiimoteStateOrigin.AccelState.Values.Y;
            wiimoteStateDestination.AccelState.Values.Z = wiimoteStateOrigin.AccelState.Values.Z;
            // Nunchuk //
            wiimoteStateDestination.NunchukState.C = wiimoteStateOrigin.NunchukState.C;
            wiimoteStateDestination.NunchukState.Z = wiimoteStateOrigin.NunchukState.Z;
            wiimoteStateDestination.NunchukState.Joystick.X = wiimoteStateOrigin.NunchukState.Joystick.X;
            wiimoteStateDestination.NunchukState.Joystick.Y = wiimoteStateOrigin.NunchukState.Joystick.Y;
            // IR //
            wiimoteStateDestination.IRState.IRSensors[0].Position = wiimoteStateOrigin.IRState.IRSensors[0].Position;
            wiimoteStateDestination.IRState.IRSensors[0].RawPosition = wiimoteStateOrigin.IRState.IRSensors[0].RawPosition;
            wiimoteStateDestination.IRState.IRSensors[0].Found = wiimoteStateOrigin.IRState.IRSensors[0].Found;
            wiimoteStateDestination.IRState.IRSensors[0].Size = wiimoteStateOrigin.IRState.IRSensors[0].Size;

            wiimoteStateDestination.IRState.IRSensors[1].Position = wiimoteStateOrigin.IRState.IRSensors[1].Position;
            wiimoteStateDestination.IRState.IRSensors[1].RawPosition = wiimoteStateOrigin.IRState.IRSensors[1].RawPosition;
            wiimoteStateDestination.IRState.IRSensors[1].Found = wiimoteStateOrigin.IRState.IRSensors[1].Found;
            wiimoteStateDestination.IRState.IRSensors[1].Size = wiimoteStateOrigin.IRState.IRSensors[1].Size;

            wiimoteStateDestination.IRState.IRSensors[2].Position = wiimoteStateOrigin.IRState.IRSensors[2].Position;
            wiimoteStateDestination.IRState.IRSensors[2].RawPosition = wiimoteStateOrigin.IRState.IRSensors[2].RawPosition;
            wiimoteStateDestination.IRState.IRSensors[2].Found = wiimoteStateOrigin.IRState.IRSensors[2].Found;
            wiimoteStateDestination.IRState.IRSensors[2].Size = wiimoteStateOrigin.IRState.IRSensors[2].Size;

            wiimoteStateDestination.IRState.IRSensors[3].Position = wiimoteStateOrigin.IRState.IRSensors[3].Position;
            wiimoteStateDestination.IRState.IRSensors[3].RawPosition = wiimoteStateOrigin.IRState.IRSensors[3].RawPosition;
            wiimoteStateDestination.IRState.IRSensors[3].Found = wiimoteStateOrigin.IRState.IRSensors[3].Found;
            wiimoteStateDestination.IRState.IRSensors[3].Size = wiimoteStateOrigin.IRState.IRSensors[3].Size;
                        
        } // CloneState

        #endregion

        #region Check Connectivity

        /// <summary>
        /// Indicates if the Wiimote remains connected to the system or not.
        /// This method is time consuming because it waits the Wiimote response.
        /// </summary>
		public void CheckConnectivity()
		{
            if (isConnected)
            {
                try
                {
                    wiimote.GetStatus();
                }
                catch (Exception)
                {
                    isConnected = false;
                    MessageBox.Show("", "Wiimote " + playerIndex + " Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        } // CheckConnectivity
		
        #endregion

        #region Render Sensors

        /// <summary>
        /// Render the four points that were found by the IR camera.
        /// </summary>
        public void RenderSensors()
        {
            if (WiimoteIRSensor0Found)
            {
                wiimoteSensorsTexture.RenderOnScreen(new Rectangle((int)(WiimoteIRSensor0Position.X * Screen.Width), (int)(WiimoteIRSensor0Position.Y * Screen.Height), 50, 50), new Rectangle(0, 0, 50, 50));
            }
            if (WiimoteIRSensor1Found)
            {
                wiimoteSensorsTexture.RenderOnScreen(new Rectangle((int)(WiimoteIRSensor1Position.X * Screen.Width), (int)(WiimoteIRSensor1Position.Y * Screen.Height), 50, 50), new Rectangle(50, 0, 50, 50));
            }
            if (WiimoteIRSensor2Found)
            {
                wiimoteSensorsTexture.RenderOnScreen(new Rectangle((int)(WiimoteIRSensor2Position.X * Screen.Width), (int)(WiimoteIRSensor2Position.Y * Screen.Height), 50, 50), new Rectangle(0, 50, 50, 50));
            }
            if (WiimoteIRSensor3Found)
            {
                wiimoteSensorsTexture.RenderOnScreen(new Rectangle((int)(WiimoteIRSensor3Position.X * Screen.Width), (int)(WiimoteIRSensor3Position.Y * Screen.Height), 50, 50), new Rectangle(50, 50, 50, 50));
            }
        } // RenderSensors

        #endregion

        #region Calculate Sensors Middle Point

        /// <summary>
        /// Function to calculate straight-line distance between two points:
        /// </summary>
        private static float Distance(PointF a, PointF b)
        {
            return (float)(Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2)));
        } // Distance

        /// <summary>
        /// Returns (in screen coordinates) the middle point of the tracking of the four IR lights by the IR camera.
        /// This algorithm take in account the possibility of doesn't found all the IR lights.
        /// </summary>
        public Microsoft.Xna.Framework.Point CalculateSensorsMiddlePointScreenCoordinates()
        {   
            PointF wiimoteCursorPosition = CalculateSensorsMiddlePoint();
            return new Microsoft.Xna.Framework.Point((int)(wiimoteCursorPosition.X * (float)(Screen.Width)), (int)(wiimoteCursorPosition.Y * (float)(Screen.Height)));
        }

        /// <summary>
        /// Returns (range 0 to 1) the middle point of the tracking of the four IR lights by the IR camera.
        /// This algorithm take in account the possibility of doesn't found all the IR lights.
        /// </summary>
        public PointF CalculateSensorsMiddlePoint()
        {
            // Try to group the IR lights in clusters.
            List<PointF> cluster1 = new List<PointF>(),
                         cluster2 = new List<PointF>();
            PointF cluster1Prom = new PointF(),
                   cluster2Prom = new PointF(),
                   result = new PointF();
            
            // For each possible light
            for (int i = 0; i < 4; i++)
            {
                if (wiimoteState.IRState.IRSensors[i].Found) // If the IR light was found
                {
                    if (cluster1.Count == 0) // put the first founded light in the first cluster 
                    {
                        cluster1.Add(wiimoteState.IRState.IRSensors[i].Position);
                    }
                    else // If it isn't the first light we need to find witch cluster is the best suited.
                    {
                        if (Distance(wiimoteState.IRState.IRSensors[i].Position, cluster1[0]) < 0.1f) 
                        {
                            cluster1.Add(wiimoteState.IRState.IRSensors[i].Position);
                        }
                        else
                        {
                            cluster2.Add(wiimoteState.IRState.IRSensors[i].Position);
                        }
                    }
                }
            }
            if (cluster1.Count == 0 || cluster2.Count == 0) // If it's impossible to triangulate the position uses the last know position
            {
                return lastPosition;
            }
            // Cluster 1 average //
            foreach (PointF point in cluster1)
            {
                cluster1Prom.X += point.X;
                cluster1Prom.Y += point.Y;
            }
            cluster1Prom.X = cluster1Prom.X / cluster1.Count;
            cluster1Prom.Y = cluster1Prom.Y / cluster1.Count;
            // Cluster 2 average //
            foreach (PointF point in cluster2)
            {
                cluster2Prom.X += point.X;
                cluster2Prom.Y += point.Y;
            }
            cluster2Prom.X = cluster2Prom.X / cluster2.Count;
            cluster2Prom.Y = cluster2Prom.Y / cluster2.Count;
            // Result X //
            result.X = (cluster1Prom.X + cluster2Prom.X) / 2;
            result.X = -(result.X - 1);             // Mirrow the result
            result.X = (result.X - 0.15f) * 1.428f; // This line can be improved a lot. It's almost try and error, almost.
            if (result.X < 0) result.X = 0;
            if (result.X > 1) result.X = 1;
            // Result Y //
            result.Y = (cluster1Prom.Y + cluster2Prom.Y) / 2;
            
            lastPosition = result;
            return result;
        } // CalculateSensorsMiddlePoint

        #endregion

        #region Set Vibration

        /// <summary>
        /// Sets the vibration on or off.
        /// </summary>
        public void SetVibration(bool on)
        {
            wiimote.SetRumble(on);
            duration = 0;
            chronometer.Pause();
        } // SetVibration
        
        /// <summary>
        /// Sets the vibration on for a specified time. Time is expressed in seconds.
        /// </summary>
        public void SetVibration(double _duration)
        {
            duration = _duration;
            
            if (duration <= 0) // I want to be langa in this one.
                wiimote.SetRumble(false);
            else // Activates vibration and chornometer
            {
                wiimote.SetRumble(true);
                chronometer.Reset();
                chronometer.Start();
            }
        } // SetVibration

        #endregion

        #region Disconnect

        /// <summary>
        /// Disconnect the wiimote
        /// </summary>
        public void Disconnect()
        {
            if (wiimote != null)
            {
                wiimote.SetRumble(false);
                wiimote.SetLEDs(0);
                //wiimote.Disconnect(); // I have problems with this method.
            }
        } // Disconnect

        #endregion

        #region Static

        #region Variables

        /// <summary>
        /// The four possible Wiimotes.
        /// </summary>
        private readonly static Wiimote wiimotePlayerOne   = new Wiimote(1),
                                        wiimotePlayerTwo   = new Wiimote(2),
                                        wiimotePlayerThree = new Wiimote(3),
                                        wiimotePlayerFour  = new Wiimote(4);

        /// <summary>
        /// Texture used to represent the four IR lights in the engine.
        /// </summary>
        private static Texture wiimoteSensorsTexture;

        #endregion

        #region Properties

        /// <summary>
        /// Wiimote assigned to player one. 
        /// </summary>
        public static Wiimote PlayerOne { get { return wiimotePlayerOne; } }

        /// <summary>
        /// Wiimote assigned to player two. 
        /// </summary>
        public static Wiimote PlayerTwo { get { return wiimotePlayerTwo; } }

        /// <summary>
        /// Wiimote assigned to player three. 
        /// </summary>
        public static Wiimote PlayerThree { get { return wiimotePlayerThree; } }

        /// <summary>
        /// Wiimote assigned to player four. 
        /// </summary>
        public static Wiimote PlayerFour { get { return wiimotePlayerFour; } }

        #endregion

        #region Init Wiimotes

        /// <summary>
        /// Initialization of all active Wiimotes.
        /// </summary>
        public static void InitWiimotes()
        {
            try
            {
                // Find all wiimotes connected to the system
                WiimoteCollection wiimoteCollection = new WiimoteCollection();
                wiimoteCollection.FindAllWiimotes();

                // For each Wiimote founded
                int index = 1;
                foreach (WiimoteLib.Wiimote wiimote in wiimoteCollection)
                {
                    // Connect to Wiimote
                    bool error = false;
                    try
                    {
                        wiimote.Connect();
                    }
                    catch (WiimoteException)
                    {
                        error = true;
                    }
                    if (!error) // If the connection was a success
                    {
                        switch (index)
                        {
                            case 1: wiimotePlayerOne.AssignWiimote(wiimote);   break;
                            case 2: wiimotePlayerTwo.AssignWiimote(wiimote);   break;
                            case 3: wiimotePlayerThree.AssignWiimote(wiimote); break;
                            case 4: wiimotePlayerFour.AssignWiimote(wiimote);  break;
                        }
                        index++;
                    }
                }
            }
            catch
            {
                // If it can't connect to any Wiimote we do nothing.
                // All problems with connections need to be address outside the application.
            }
            // Creates the texture
            wiimoteSensorsTexture = new Texture("WiimoteSensors");
        } // Wiimote

        #endregion

        #endregion

    } // Wiimote
} // XNAFinalEngine.Input
