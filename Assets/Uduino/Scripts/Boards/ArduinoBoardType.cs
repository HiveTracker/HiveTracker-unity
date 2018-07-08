using System.Collections.Generic;

/// <summary>
/// For the moment this is not used
/// </summary>
namespace Uduino
{
    #region Board types
    public class ArduinoBoardType
    {
        public string name = "";
        Dictionary<string, int> pins = new Dictionary<string, int>();

        //                                                                      TODO : If we add this as optional value, is it breaking ?
        public ArduinoBoardType(string name, int numberDigital, int numberAnalog, int[] otherAnalogPins)
        {
            int totalPin = 0;
            this.name = name;
            for (int i = 0; i <= numberDigital; i++)
            {
                totalPin++;
                pins.Add("" + i, totalPin);

            }
            for (int i = 0; i <= numberAnalog; i++)
            {
                totalPin++;
                pins.Add("A" + i, totalPin);
            }
            if (otherAnalogPins != null)
            {
                for (int i = 0; i <= otherAnalogPins.Length; i++)
                {
                    string key = "A" + (numberAnalog + i);
                    if (!pins.ContainsKey(key))
                        pins.Add(key, otherAnalogPins[i]);
                }
            }
        }

        public string[] GetPins()
        {
            string[] keys = new string[pins.Keys.Count];
            pins.Keys.CopyTo(keys, 0);
            return keys;
        }

        public int GetPin(int id)
        {
            return GetPin(id + "");
        }

        public int GetPin(string id)
        {
            if (id[0] == 'd' || id[0] == 'D') id = id.Remove(0, 1);
            int outValue = -1;
            bool hasFound = pins.TryGetValue(id.ToUpper(), out outValue);
            if (!hasFound)
                Log.Error("The pin " + id + " does not exists for the " + name);
            return outValue;
        }
    }
    public class BoardsTypeList
    {
        private static BoardsTypeList _boards = null;
        public static BoardsTypeList Boards
        {
            get
            {
                if (_boards != null)
                    return _boards;
                else
                {
                    _boards = new BoardsTypeList();
                    return _boards;
                }
            }
            set
            {
                if (Boards == null)
                    _boards = value;
            }
        }
        public List<ArduinoBoardType> boardTypes = new List<ArduinoBoardType>();

        BoardsTypeList()
        {
            boardTypes.Add(new ArduinoBoardType("Arduino Uno", 13, 6, null));
            boardTypes.Add(new ArduinoBoardType("Arduino Duemilanove", 13, 6, null));
            boardTypes.Add(new ArduinoBoardType("Arduino Leonardo", 13, 6, null));
            boardTypes.Add(new ArduinoBoardType("Arduino Pro Mini", 13, 6, null));
            boardTypes.Add(new ArduinoBoardType("Arduino Mega", 53, 15, null));
            boardTypes.Add(new ArduinoBoardType("Arduino Due", 53, 13, null));
            boardTypes.Add(new ArduinoBoardType("Arduino Nano", 13, 8, null));
            boardTypes.Add(new ArduinoBoardType("Arduino Mini", 13, 7, null));
            //   boardTypes.Add(new ArduinoBoardType("Arduino Yun", 13, 6, new int[] {4,6,7,8,9,10,12}));
        }

        /// <summary>
        /// List the arduino boards as an array
        /// </summary>
        /// <returns>Array of arduino boards</returns>
        public string[] ListToNames()
        {
            List<string> names = new List<string>();
            boardTypes.ForEach(x => names.Add(x.name));
            return names.ToArray();
        }
        /// <summary>
        /// Return the arduino board type from a name
        /// </summary>
        /// <param name="name">Name of the board</param>
        /// <returns>ArduinoBoardType</returns>
        public ArduinoBoardType GetBoardFromName(string name)
        {
            return boardTypes.Find(x => x.name == name);
        }

        /// <summary>
        /// Return the arduino board type from an id
        /// </summary>
        /// <param boardId="boardId">Name of the board</param>  
        /// <returns>ArduinoBoardType</returns>
        public ArduinoBoardType GetBoardFromId(int boardId)
        {
            return boardTypes[boardId];
        }

        /// <summary>
        /// Return the arduino board ID from a name
        /// </summary>
        /// <param name="name">Name of the board</param>
        /// <returns>Aarduino board index in List</returns>
        public int GetBoardIdFromName(string name)
        {
            ArduinoBoardType board = boardTypes.Find(x => x.name == name);
            return boardTypes.IndexOf(board);
        }


        /// <summary>
        /// Add a new board type
        /// </summary>
        /// <param name="name">Name of the custom board</param>
        /// <param name="numberDigital">Number of digital pins</param>
        /// <param name="numberAnalog">Number of analog pin</param>
        /// <returns>Return new boardType</returns>
        public ArduinoBoardType addCustomBoardType(string name, int numberDigital, int numberAnalog, int[] otherAnalogPins)
        {
            ArduinoBoardType board = new ArduinoBoardType(name, numberDigital, numberAnalog, otherAnalogPins);
            boardTypes.Add(board);
            return board;
        }
    }
    #endregion
}