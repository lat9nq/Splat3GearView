﻿using Splat3GearView.Properties;
using Splat3GearView.Resources;
using Splat3GearView.Structures;
using SysBot.Base;
using System.Net.Sockets;
using System.Text.Json;

namespace Splat3GearView
{
    public partial class MainWindow : Form
    {
        private readonly static SwitchConnectionConfig Config = new() { Protocol = SwitchProtocol.WiFi, IP = Settings.Default.SwitchIP, Port = 6000 };
        public SwitchSocketAsync SwitchConnection = new(Config);

        public readonly GearData GearData = new();

        private readonly static List<Gear> GearList = new();
        private int index;

        public MainWindow()
        {
            string build = string.Empty;
#if DEBUG
            var date = File.GetLastWriteTime(System.Reflection.Assembly.GetEntryAssembly()!.Location);
            build = $" (dev-{date:yyyyMMdd})";
#endif
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
            Text = "Splat3GearView v" + v.Major + "." + v.Minor + "." + v.Build + build;

            InitializeComponent();
        }

        private void MainWindow_Load(object sender, EventArgs e)
        {
            InputSwitchIP.Text = Settings.Default.SwitchIP;
            LabelIndex.Text = string.Empty;
        }

        private void InputSwitchIP_Changed(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            if (textBox.Text != "192.168.0.0")
            {
                Settings.Default.SwitchIP = textBox.Text;
                Config.IP = textBox.Text;
            }
            Settings.Default.Save();
        }

        private async void Connect_ClickAsync(object sender, EventArgs e)
        {
            try
            {
                ConnectionStatusText.Text = "Connecting...";
                SwitchConnection.Connect();
                index = 0;

                ButtonConnect.Enabled = false;
                ButtonDisconnect.Enabled = true;
                ConnectionStatusText.Text = "Reading gear...";
                GearList.Clear();
                uint pos = 0x40; // First gear is located at offset + 0x40
                Gear gear;

                // Load Headgear
                ConnectionStatusText.Text = "Reading headgear...";
                do
                {
                    var Data = await SwitchConnection.ReadBytesAsync(Offsets.GearList_Head + pos, Gear.SIZE, CancellationToken.None); // Read gear
                    gear = new Gear(Data)
                    {
                        GearType = (byte)GearTypes.Headgear
                    };
                    if (gear.IsValid)
                    {
                        gear.SetInfoFromJSON();
                        GearList.Add(gear);
                    }
                    pos += Gear.SIZE; // Iterate through list
                } while (gear.IsValid); // Reached junk data/end of list

                // Load Clothes
                pos = 0x40;
                ConnectionStatusText.Text = "Reading clothes...";
                do
                {
                    var Data = await SwitchConnection.ReadBytesAsync(Offsets.GearList_Clothes + pos, Gear.SIZE, CancellationToken.None);
                    gear = new Gear(Data)
                    {
                        GearType = (byte)GearTypes.Clothes
                    };
                    if (gear.IsValid)
                    {
                        gear.SetInfoFromJSON();
                        GearList.Add(gear);
                    }
                    pos += Gear.SIZE;
                } while (gear.IsValid);

                // Load Shoes
                pos = 0x40;
                ConnectionStatusText.Text = "Reading shoes...";
                do
                {
                    var Data = await SwitchConnection.ReadBytesAsync(Offsets.GearList_Shoes + pos, Gear.SIZE, CancellationToken.None);
                    gear = new Gear(Data)
                    {
                        GearType = (byte)GearTypes.Shoes
                    };
                    if (gear.IsValid)
                    {
                        gear.SetInfoFromJSON();
                        GearList.Add(gear);
                    }
                    pos += Gear.SIZE;
                } while (gear.IsValid);

                LabelLoadedGear.Text = $"Loaded Gear: {GearList.Count}";
                LabelLoadedHeadgear.Text = $"Headgear: {GearList.Where(g => g.GearType == (byte)GearTypes.Headgear).Count()}";
                LabelLoadedClothes.Text = $"Clothes: {GearList.Where(g => g.GearType == (byte)GearTypes.Clothes).Count()}";
                LabelLoadedShoes.Text = $"Shoes: {GearList.Where(g => g.GearType == (byte)GearTypes.Shoes).Count()}";
                if (GearList.Count > 0)
                {
                    DisplayGear(index);
                    ButtonDumpGear.Enabled = true;
                }
                else ButtonDumpGear.Enabled = false;
                Disconnect();
            }
            catch (SocketException err)
            {
                // a bit hacky but it works
                if (err.Message.Contains("failed to respond") || err.Message.Contains("actively refused"))
                {
                    ConnectionStatusText.Text = "Unable to connect.";
                    MessageBox.Show(err.Message);
                }
                else
                {
                    ConnectionStatusText.Text = "Disconnected.";
                }
                ButtonConnect.Enabled = true;
                ButtonDisconnect.Enabled = false;
            }
        }

        private void Disconnect_Click(object sender, EventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            if (SwitchConnection.Connected)
            {
                SwitchConnection.Disconnect();
                ConnectionStatusText.Text = "Disconnected.";
                ButtonConnect.Enabled = true;
                ButtonDisconnect.Enabled = false;
            }
        }

        private void DisplayGear(int num)
        {
            LabelIndex.Text = $"{(index + 1):D2} / {GearList.Count}";
            Gear gear = GearList[num];
            GearName.Text = gear.Name;
            GearBrand.Text = gear.Brand;
            GearID.Text = $"{gear.ID:D5}";
            GearSeed.Text = $"{gear.Seed:X8}";
            GearRarity.Text = $"{gear.Rarity}";
            GearSlots.Text = $"{gear.Ability_SubCount}";
            GearPrimaryAbility.Text = Strings.Abilities[gear.Ability_Primary];
            GearSubAbility1.Text = Strings.Abilities[gear.Ability_Sub1];
            GearSubAbility2.Text = gear.Rarity >= 1 ? Strings.Abilities[gear.Ability_Sub2] : "(None)";
            GearSubAbility3.Text = gear.Rarity >= 2 ? Strings.Abilities[gear.Ability_Sub3] : "(None)";
        }

        private void ButtonPrevious_Click(object sender, EventArgs e)
        {
            if (GearList.Count > 0)
            {
                index = (index + GearList.Count - 1) % GearList.Count; // Wrap around
                DisplayGear(index);
            }
        }

        private void ButtonNext_Click(object sender, EventArgs e)
        {
            if (GearList.Count > 0)
            {
                index = (index + GearList.Count + 1) % GearList.Count; // Wrap around
                DisplayGear(index);
            }
        }

        private readonly static Dictionary<string, Dictionary<string, WebsiteFormat>> WebsiteJSON = new();
        private void ButtonDumpGear_Click(object sender, EventArgs e)
        {
            WebsiteJSON["HaveGearHeadMap"] = new();
            WebsiteJSON["HaveGearClothesMap"] = new();
            WebsiteJSON["HaveGearShoesMap"] = new();

            foreach (Gear gear in GearList)
            {
                string key = gear.GearType switch
                {
                    (byte)GearTypes.Headgear => "HaveGearHeadMap",
                    (byte)GearTypes.Clothes => "HaveGearClothesMap",
                    _ => "HaveGearShoesMap",
                };
                WebsiteJSON[key][$"{gear.ID}"] = new()
                {
                    MainSkill = // Website format is weird
                        gear.Ability_Primary - 1 >= (byte)Abilities.IntensifyAction
                        ? (gear.Ability_Primary - (byte)Abilities.IntensifyAction) + 99
                        : gear.Ability_Primary - 1,
                    RandomContext = gear.Seed,
                    ExDrinksArray = Array.Empty<int>(),
                    ExSkillArray = new int[] { gear.Ability_Sub1 - 1, gear.Ability_Sub2 - 1, gear.Ability_Sub3 - 1 }
                };
            }

            string JSONString = JsonSerializer.Serialize(WebsiteJSON);
            Clipboard.SetText(JSONString);
            MessageBox.Show("Copied JSON to clipboard!\nUpload it at https://leanny.github.io/splat3seedchecker/#/settings");
        }
    }
}
