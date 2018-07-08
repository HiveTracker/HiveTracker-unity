using System;

namespace Uduino { 

    public class UduinoDevice_AndroidBluetoothLE : UduinoDevice {

        UduinoConnection _connection;

        public UduinoDevice_AndroidBluetoothLE() : base() { }

        public UduinoDevice_AndroidBluetoothLE(UduinoConnection connection) : base()
        {
            _connection = connection;
            this.identity = "uduinoBLE";
        }

        public UduinoDevice_AndroidBluetoothLE(UduinoConnection connection, string identity) : base()
        {
            _connection = connection;
            this.identity = identity;
        }

        /// <summary>
        /// Open a specific serial port
        /// </summary>
        public override void Open()
        {
            boardStatus = BoardStatus.Open;
            Log.Info("Opening BLE Device");
        }

        public override void UduinoFound()
        {
            base.UduinoFound();
            BluetoothInterface.Instance.UduinoConnected(this.name);
        }

        #region Commands
        /// <summary>
        /// Loop every thead request to write a message on the arduino (if any)
        /// </summary>
        public override bool WriteToArduinoLoop()
        {
            lock (writeQueue)
            {
                if (writeQueue.Count == 0)
                    return false;

                string message = (string)writeQueue.Dequeue();
                try
                {
                    try
                    {
                        _connection.PluginWrite(message);
                    }
                    catch (Exception)
                    {
                        writeQueue.Enqueue(message);
                        return false;
                    }
                }
                catch (Exception e)
                {
                    Log.Error("Error on port <color=#2196F3>[" + "" + "]</color> : " + e);
                    // Close();
                    return false;
                }
                WritingSuccess(message);
            }
            return true;
        }

        /// <summary>
        /// Read Arduino serial port
        /// </summary>
        /// <param name="message">Write a message to the serial port before reading the serial</param>
        /// <param name="instant">Read the message value now and not in the thread loop</param>
        /// <returns>Read data</returns>
        public override string ReadFromArduino(string message = null, bool instant = false)
        {
            return base.ReadFromArduino(message, instant);
        }

        public override bool ReadFromArduinoLoop(bool forceReading = false)
        {
            return base.ReadFromArduinoLoop(forceReading);
        }
        #endregion

    }
}
