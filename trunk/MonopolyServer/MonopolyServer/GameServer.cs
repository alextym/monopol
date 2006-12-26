using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Specialized;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.Reflection;

namespace Monopoly
{
    abstract class Field
    {
        //public enum FieldType {Go, City, CommunityChest, IncomeTax, Railroad, Chance, Jail, Utility,
          //  FreeParking, GoToJail, LuxuryTax}
        //public FieldType Type;
        public string Name;
        public int Id;

        public abstract void ServerAction(PlayerInfo aPlayer, GameServer aServer, int aDice1, int aDice2);
    }

    abstract class Property : Field
    {
        public PropertyGroup Group;
        public Player Owner = null;
        public int Price;
        public bool Mortgaged = false;
        public int MortgageValue()
        {
            return Price / 2;
        }
        public int UnmortgageValue()
        {
            return (int)(1.1 * (Price / 2));
        }
        public abstract int CalculateRent(int aDice1, int aDice2);
        public override void ServerAction(PlayerInfo aPlayer, GameServer aServer, int aDice1, int aDice2)
        {
            PlayerInfo src;
            XmlElement msg;
            if (Owner == null)
            {
                aServer.SendMessage(aPlayer, "<buyVisitedProperty id=\"" + Id + "\"/>");
                
                for(;;)
                {
                    aServer.ProcessGetNextMessage(out src, out msg);
                    if(src == aPlayer && msg.LocalName == "buyVisitedProperty")
                    {
                        switch(msg.GetAttribute("buy"))
                        {
                            case "true":
                                Owner = aPlayer.MyPlayer;
                                aPlayer.MyPlayer.Money -= Price;
                                aPlayer.MyPlayer.OwnedProperties.Add(this);
                                return;
                            case "false":
                                return;
                            default:
                                Console.WriteLine("Protocol error! (" + src.MyPlayer.Nickname + ")");
                                break;
                        }
                    }
                    else
                        Console.WriteLine("Protocol error! (" + src.MyPlayer.Nickname + ")");
                }

                
            }
            
                     
                
        }
    }

    class PropertyGroup
    {
        public PropertyGroup(string aName)
        {
            Name = aName;
        }

        public string Name;
        public Player GetOwner()
        {
            Player o = null;
            foreach(Property p in Properties)
            {
                if(o == null)
                    o = p.Owner;
                else if(o != p.Owner)
                    return null;
            }

            return o;
        }

        public List<Property> Properties = new List<Property>();
    }


    class City : Property
    {
        public int NumHouses = 0; //5 = 1 hotel itd.
        public int[] Rents;
        int PricePerHouse;
        public override int CalculateRent(int aDice1, int aDice2)
        {
            if (Owner == null || Mortgaged)
                return 0;

            return (NumHouses / Rents.Length) * Rents[Rents.Length - 1] //hotele
                + (NumHouses % Rents.Length) * Rents[NumHouses % Rents.Length]; //domy
        }

        public City(int aId, string aName, PropertyGroup aCountry, int aPrice, int aPricePerHouse, int[] aRents)
        {
            Id = aId;
            Name = aName;
            Price = aPrice;
            Group = aCountry;
            aCountry.Properties.Add(this);
            PricePerHouse = aPricePerHouse;
            Rents = aRents;
        }
    }

    class RailRoad : Property
    {
        public override int CalculateRent(int aDice1, int aDice2)
        {
            if (Owner == null || Mortgaged)
                return 0;           

            int ownedRailroads = 0;
            foreach (Property p in Group.Properties)
                if (p.Owner == Owner)
                    ownedRailroads++;

            switch (ownedRailroads)
            {
                case 1: return 25;
                case 2: return 50;
                case 3: return 100;
                case 4: return 200;
            }

            throw new Exception("Internal error");
            //return 0;
        }

        public RailRoad(int aId, string aName, PropertyGroup aGroup)
        {
            Id = aId;
            Name = aName;
            Group = aGroup;
            Price = 200;
        }
    }

    class Utility : Property
    {
        public Utility(int aId, string aName, PropertyGroup aGroup)
        {
            Id = aId;
            Name = aName;
            Price = 150;
            Group = aGroup;
        }

        public override int CalculateRent(int aDice1, int aDice2)
        {
            if (Owner == null || Mortgaged)
                return 0;

            if (Group.GetOwner() == Owner)
                return (aDice1 + aDice2) * 10;
            else
                return (aDice1 + aDice2) * 4;
        }


    }
    
    class DoNothingField : Field
    {
        public override void ServerAction(PlayerInfo aPlayer, GameServer aServer, int aDice1, int aDice2)
        {
        }

        public DoNothingField(int aId)
        {
            Id = aId;
        }
    }
     

    /*
    class Card
    {
        public abstract void ServerAction(Player aPlayer, int aDice1, int aDice2);
    }

    class GetOutOfJailFreeCard
    {
        public virtual void ServerAction(Player aPlayer, int aDice1, int aDice2)
        {
        }
    }
      */  

    class Board
    {
        //Stan planszy, klient powinie go zmieniaŠ tylko poťrednio, wysy│aj╣c polecenia przez sieŠ
        public Field[] Fields;
        public List<Card> CommunityChest;
        public List<Card> CommunityChestUsed;
        public List<Card> Chances;
        public List<Card> ChancesUsed;
        public Dictionary<string, PropertyGroup> PropertyGroups = new Dictionary<string, PropertyGroup>();

        public Board()
        {
            PropertyGroups.Add("1", new PropertyGroup("1"));
            PropertyGroups.Add("2", new PropertyGroup("1"));
            PropertyGroups.Add("3", new PropertyGroup("1"));
            PropertyGroups.Add("4", new PropertyGroup("1"));
            PropertyGroups.Add("5", new PropertyGroup("1"));
            PropertyGroups.Add("6", new PropertyGroup("1"));
            PropertyGroups.Add("7", new PropertyGroup("1"));
            PropertyGroups.Add("8", new PropertyGroup("1"));
            PropertyGroups.Add("Railroads", new PropertyGroup("Railroads"));
            PropertyGroups.Add("Utilities", new PropertyGroup("Utilities"));

            Fields = new Field[]
            {
                new DoNothingField(0), 
                new City(1, "1-1", PropertyGroups["1"], 60, 50, new int[]{2, 10, 30, 90, 160, 250}),
                new ChanceField(2, CommunityChest, CommunityChestUsed),
                new City(3, "1-2", PropertyGroups["1"], 60, 50, new int[]{4, 20, 60, 180, 320, 450}),
                new IncomeTaxField(4),
                new RailRoad(5, "Railroads-1", PropertyGroups["Railroads"]),
                new City(6, "2-1", PropertyGroups["2"], 100, 50, new int[]{6, 30, 90, 270, 400, 550}),
                new ChanceField(7, Chances, ChancesUsed),
                new City(8, "2-2", PropertyGroups["2"], 100, 50, new int[]{6, 30, 90, 270, 400, 550}),
                new City(9, "2-3", PropertyGroups["2"], 120, 50, new int[]{8, 40, 100, 300, 450, 600}),
                new DoNothingField(10), 
                new City(11, "3-1", PropertyGroups["3"], 140, 100, new int[]{10, 50, 150, 450, 625, 750}),
                new Utility(12, "Electric Company", PropertyGroups["Utilities"]),
                new City(13, "3-2", PropertyGroups["3"], 140, 100, new int[]{10, 50, 150, 450, 625, 750}),
                new City(14, "3-3", PropertyGroups["3"], 160, 100, new int[]{12, 60, 180, 500, 700, 900}),
                new RailRoad(15, "Railroads-2", PropertyGroups["Railroads"]),
                new City(16, "4-1", PropertyGroups["4"], 180, 100, new int[]{14, 70, 200, 550, 750, 950}),
                new ChanceField(17, CommunityChest, CommunityChestUsed),
                new City(18, "4-2", PropertyGroups["4"], 180, 100, new int[]{14, 70, 200, 550, 750, 950}),
                new City(19, "4-3", PropertyGroups["4"], 200, 100, new int[]{16, 80, 220, 600, 800, 1000}),
                new DoNothingField(20),
                new City(21, "5-1", PropertyGroups["5"], 220, 150, new int[]{18, 90, 250, 700, 875, 1050}),
                new ChanceField(22, Chances, ChancesUsed),
                new City(23, "5-2", PropertyGroups["5"], 220, 150, new int[]{18, 90, 250, 700, 875, 1050}),
                new City(24, "5-3", PropertyGroups["5"], 240, 150, new int[]{20, 100, 300, 750, 925, 1100}),
                new RailRoad(25, "Railroads-3", PropertyGroups["Railroads"]),
                new City(26, "6-1", PropertyGroups["6"], 260, 150, new int[]{22, 110, 330, 800, 975, 1150}),
                new City(27, "6-2", PropertyGroups["6"], 260, 150, new int[]{22, 110, 330, 800, 975, 1150}),
                new Utility(28, "Waterworks", PropertyGroups["Utilities"]),
                new City(29, "6-3", PropertyGroups["6"], 280, 150, new int[]{24, 120, 360, 850, 1025, 1200}),
                new GoToJailField(30),
                new City(31, "7-1", PropertyGroups["7"], 300, 200, new int[]{26, 130, 390, 900, 1100, 1275}),
                new City(32, "7-2", PropertyGroups["7"], 300, 200, new int[]{26, 130, 390, 900, 1100, 1275}),
                new ChanceField(33, CommunityChest, CommunityChestUsed),
                new City(34, "7-3", PropertyGroups["7"], 320, 200, new int[]{28, 150, 450, 1000, 1200, 1400}),
                new RailRoad(35, "Railroads-4", PropertyGroups["Railroads"]),
                new ChanceField(36, Chances, ChancesUsed),
                new City(37, "8-1", PropertyGroups["8"], 350, 200, new int[]{35, 175, 500, 1100, 1300, 1500}),
                new LuxuryTaxField(38),
                new City(39, "8-2", PropertyGroups["8"], 400, 200, new int[]{50, 200, 600, 1400, 1700, 2000})
            };
        }

    }

    class ChanceField : Field
    {
        public override void ServerAction(PlayerInfo aPlayer, GameServer aServer, int aDice1, int aDice2)
        {
        }

        public List<Card> Cards;
        public List<Card> UsedCards;

        public ChanceField(int aId, List<Card> aCards, List<Card> aUsedCards)
        {
            Id = aId;
            Cards = aCards;
            UsedCards = aUsedCards;
        }
    }

    class IncomeTaxField : Field
    {
        public override void ServerAction(PlayerInfo aPlayer, GameServer aServer, int aDice1, int aDice2)
        {
        }

        public IncomeTaxField(int aId)
        {
            Id = aId;
        }
    }

    class LuxuryTaxField : Field
    {
        public override void ServerAction(PlayerInfo aPlayer, GameServer aServer, int aDice1, int aDice2)
        {
        }

        public LuxuryTaxField(int aId)
        {
            Id = aId;
        }
    }

    class GoToJailField : Field
    {
        public override void ServerAction(PlayerInfo aPlayer, GameServer aServer, int aDice1, int aDice2)
        {
        }

        public GoToJailField(int aId)
        {
            Id = aId;
        }
    }



    class Player
    {
        /*
        public Player(Socket aSocket)
        {
            Connection = aSocket;
  
        }*/
        public bool Ready = false;
        public string Nickname;
        public int Position = 0;
        
       // public Bitmap Token;
        //public Socket Connection;
       // public PlayerController Controller;
        public int Money = 1500;
        public int TurnsToLeaveJail = 0; //0->na wolnoťci
        public List<Property> OwnedProperties = new List<Property>();
        List<GetOutOfJailFreeCard> GetOutOfJailFreeCards = new List<GetOutOfJailFreeCard>();
    }
/*
    class Property//miasto, elektrownia...
    {
        public int Id; //na potrzeby sieci, identyfikuje i pole i nieruchomoťŠ 
        public Player Owner;
        public int Price;
        public int NumHouses;
        public int[] HousePrices;
        public int[] Rents;

        //public int CalculateRent();
        
    }
        */
    class Card
    {
        void DoAction()
        {
        }
    }

    class GetOutOfJailFreeCard : Card
    {
    }

    class MessageQueue
    {
        public MessageQueue(Socket aSocket)
        {
            mSocket = aSocket;
        }

        Socket mSocket;
        Queue<string> mMessages = new Queue<string>();
        List<byte> mReadBytes = new List<byte>();


        void Read()
        {
            if (mSocket.Available > 0)
            {
                byte[] buf = new byte[mSocket.Available];
                mSocket.Receive(buf);
                mReadBytes.AddRange(buf);
            }


        Again:
            int i = 0;

            while (i < mReadBytes.Count - 1)
            {
                byte b1, b2;
                b1 = mReadBytes[i];
                b2 = mReadBytes[i + 1];
                //if (b1 == 0 && b2 == 0)
                if (b1 == 13 && b2 == 10)
                {
                    byte[] buf = new byte[i];
                    mReadBytes.CopyTo(0, buf, 0, i);
                    mReadBytes.RemoveRange(0, i + 2);


                    mMessages.Enqueue(GameServer.IsoToUnicode(buf));
                    Console.WriteLine("Message from " + mSocket.RemoteEndPoint + ":" +
                        GameServer.IsoToUnicode(buf));

                    goto Again;
                }
                i += 1;
            }
        }

        public string Pop()
        {
            Read();
            return mMessages.Dequeue();
        }

        public int Count
        {
            get
            {
                Read();
                return mMessages.Count;
            }
        }


    }


    class PlayerInfo
    {
        public Player MyPlayer;
        public MessageQueue MyMessageQueue;
        public Socket Connection;
        
    }


    class GameServer
    {
        //List<Player> mPlayers = new List<Player>();
        //List<List<string>> mMessages = new List<List<string>>();
        //List<MessageQueue> mMessageQueues = new List<MessageQueue>();
        public List<PlayerInfo> PlayerInfos = new List<PlayerInfo>();
        public Board GameBoard = new Board();
        uint mMaxPlayers;
        ushort mPort;
        Socket mSocket;
        string mWelcomeMessage;

        public GameServer(ushort aPort, uint aMaxPlayers, string aWelcomeMessage)
        {
            mPort = aPort;
            mMaxPlayers = aMaxPlayers;
            mWelcomeMessage = aWelcomeMessage;
        }











        //NETWORK

        public static byte[] UnicodeToIso(string aStr)
        {
            Encoding iso = Encoding.GetEncoding("iso8859-2");
            byte[] buf = new byte[aStr.Length * 2];
            Buffer.BlockCopy(aStr.ToCharArray(), 0, buf, 0, aStr.Length * 2);
            return Encoding.Convert(Encoding.Unicode, iso, buf, 0, buf.Length);
        }

        public static string IsoToUnicode(byte[] aBuf)
        {
            Encoding iso = Encoding.GetEncoding("iso8859-2");
            byte[] buf = Encoding.Convert(iso, Encoding.Unicode, aBuf);
            char[] charBuf = new char[buf.Length / 2];
            Buffer.BlockCopy(buf, 0, charBuf, 0, buf.Length);
            return new string(charBuf);
        }
  
        public void SendMessage(Socket aSocket, string aMsg)
        {
            byte[] buf = new byte[aMsg.Length + 2];
            byte[] encoded = UnicodeToIso(aMsg);
            Buffer.BlockCopy(encoded, 0, buf, 0, aMsg.Length);
            buf[buf.Length - 2] = 13;
            buf[buf.Length - 1] = 10;
                      

            aSocket.Send(buf);
            Console.WriteLine("Message to " + aSocket.RemoteEndPoint + ":" + aMsg);
        }

        public void SendMessage(PlayerInfo aPlayer, string aMsg)
        {
            SendMessage(aPlayer.Connection, aMsg);
        }

        public void SendMessage(string aMsg)
        {
            foreach(PlayerInfo pi in PlayerInfos)
            {
                SendMessage(pi, aMsg);
            }
        }

        //Przetwarza wiadomość chat (niejako w tle). Inny rodzaj wiadomości parsuje i zwraca. Funkcja blokująca!
        public void ProcessGetNextMessage(out PlayerInfo aFrom, out XmlElement aMsg)
        {
            for(;;)
            {
                foreach (PlayerInfo pi in PlayerInfos)
                {
                    while (pi.MyMessageQueue.Count != 0)
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(pi.MyMessageQueue.Pop());
                        XmlElement e = (XmlElement)doc.FirstChild;
                        if (e.LocalName == "chat")
                        {
                            SendMessage("<chat from=\"" + pi.MyPlayer.Nickname + "\" message=\"" +
                                e.GetAttribute("message") + "\"/>");
                        }
                        else
                        {
                            aMsg = (XmlElement)doc.FirstChild;
                            aFrom = pi;
                            return;
                        }
                    }
                }
                System.Threading.Thread.Sleep(100);
            }
        }







        //GAME
        public void Run()
        {
            Console.WriteLine("Starting server...");
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, mPort);
            mSocket = new Socket(AddressFamily.InterNetwork,
                               SocketType.Stream, ProtocolType.Tcp);
            mSocket.Blocking = false;
            mSocket.Bind(localEndPoint);
            mSocket.Listen(10);


            //Czekamy na zgłoszenie gotowości wszystkich graczy
            int numReady = 0;
            while (numReady < PlayerInfos.Count || numReady == 0)
            {
                Socket client = null;

                try
                {
                    client = mSocket.Accept();
                }
                catch (Exception)
                {
                }
                                
                if (client != null)
                {
                    PlayerInfo pi = new PlayerInfo();
                    pi.MyPlayer = new Player();
                    pi.MyMessageQueue = new MessageQueue(client);
                    pi.Connection = client;
                    PlayerInfos.Add(pi);

                    AssemblyName an = new AssemblyName(Assembly.GetExecutingAssembly().FullName);
                        
                    SendMessage(client, "<welcome serverVersion=\""+an.Version+
                        "\" message=\"" + mWelcomeMessage + "\">");
                }

                foreach(PlayerInfo pi in PlayerInfos)
                {
                    MessageQueue q = pi.MyMessageQueue;
                    while (q.Count != 0)
                    {
                        string msg = q.Pop();
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(msg);
                        XmlElement e = (XmlElement)doc.FirstChild;

                        switch(e.LocalName)
                        {
                            case "setNick": 
                                pi.MyPlayer.Nickname = e.GetAttribute("nick");
                                break;
                            case "ready":
                                if(!pi.MyPlayer.Ready)
                                {
                                    pi.MyPlayer.Ready = true;
                                    numReady++;
                                }
                                break;
                            case "chat":
                                SendMessage("<chat from=\"" + pi.MyPlayer.Nickname + "\" message=\"" 
                                    + e.GetAttribute("message") + "\"/>");
                                break;

                        }

                        
                    }
                }

            }


            //Rozpoczynamy grę
            SendMessage("<allReady/>");
            Console.WriteLine("Starting game!");

            Random rand = new Random();
            int curPlayer = rand.Next(0, PlayerInfos.Count - 1);
            while(true)
            {
                PlayerInfo pi = PlayerInfos[curPlayer];
                          
                int dice1 = rand.Next(1, 6);
                int dice2 = rand.Next(1, 6);
                int dstPos = (pi.MyPlayer.Position + dice1 + dice2) % 40;
                SendMessage("<move player=\"" + pi.MyPlayer.Nickname + "\" dice1=\"" + dice1 + "\" dice2=\"" + dice2 +
                    "\" srcPos=\"" + pi.MyPlayer.Position + "\" dstPos=\"" + dstPos + "\"/>");
                pi.MyPlayer.Position = dstPos;

                GameBoard.Fields[dstPos].ServerAction(pi, this, dice1, dice2);

                //czas na kupno/sprzedaż itp.
                SendMessage("<freeMove player=\""+pi.MyPlayer.Nickname+"\"/>");
                for (; ; )
                {
                    PlayerInfo src; XmlElement msg;
                    ProcessGetNextMessage(out src, out msg);
                    if (src == pi && msg.LocalName == "done")
                        break;
                    else
                        Console.WriteLine("Protocol error (" + src.Connection.RemoteEndPoint + ")");
                }

            }

            
        }
              

        static void Main(string[] args)
        {
            GameServer gs = new GameServer(8000, 4, "Witamy na serwerze");
            gs.Run();
        }
    }
}
