using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using System.Reflection;
using DSharpPlus.EventArgs;
using Newtonsoft.Json;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using DSharpPlus.Entities;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
//Testing
namespace bazillionaire
{
    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set;}

        [JsonProperty("prefix")]
        public string Prefix { get; private set;}

        [JsonProperty("JsonTest")]
        public string JsonTest { get; private set;}
    }
    class BazillionBot
    {
        public BazillionBot()
        {
            // ======= Get configuration information and apply it to make a new client
            string json = File.ReadAllText(PathToConfig + "SCREAMConfig.json");
            ConfigJson configJson = JsonConvert.DeserializeObject<ConfigJson>(json);

            Console.WriteLine("Got this Token: ");
            Console.WriteLine(configJson.Token);
            Console.WriteLine("Got this prefixes: ");
            Console.WriteLine(configJson.Prefix);


            DiscordConfiguration config = new DiscordConfiguration
            {
                Token = configJson.Token,
                TokenType = TokenType.Bot,
                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };
            Client = new DiscordClient(config);

            //====== Setup event listeners with handlers
            Client.Ready += OnClientReady;
            var commandsConfig = new CommandsNextConfiguration()
            {
                StringPrefixes = new string[] { configJson.Prefix },
                EnableMentionPrefix = true,
                EnableDms = true
                //aaaa
            };

            Commands = Client.UseCommandsNext(commandsConfig);
            theWorld = new world();

        }
        public DiscordClient Client { get; private set; }
        public CommandsNextExtension Commands { get; private set; }
        public const string PathToConfig = "ConfigStuff/";
        world theWorld;
        public async Task RunAsync()
        {
            Client.MessageCreated += AddPlayer;
            Client.MessageCreated += theWorld.Status;
            Client.MessageCreated += theWorld.Options;
            Client.MessageCreated += theWorld.Sell;
            Client.MessageCreated += theWorld.Buy;
            Client.MessageCreated += theWorld.TravelTo;
            Client.MessageCreated += theWorld.Upgrade;
            Client.MessageCreated += theWorld.Map;
            Client.MessageCreated += theWorld.Help;
            await Client.ConnectAsync();
            while(true)
            {
                theWorld.tickWorld();
                await Task.Delay(10000);
            }
        }
        private Task AddPlayer(MessageCreateEventArgs e)
        {
                if (e.Message.Content.StartsWith("Add") || e.Message.Content.StartsWith("add"))
                {
                    bool alreadyAdded = false;
                    if (e.Message.Author.Username == "Mad") //Never add the bot to the list of players
                        alreadyAdded = true;
                    foreach(player checkUser in theWorld.players)
                    {
                        if(checkUser.thePlayer.Username == e.Message.Author.Username)
                        {
                            alreadyAdded = true;
                            e.Message.RespondAsync($"{e.Message.Author.Username}, you are already on the list of players.");
                        }
                    }
                    if (!alreadyAdded)
                    {
                        theWorld.players.Add(new player(e.Message.Author, theWorld.planets[4]));
                        e.Message.RespondAsync($"Sucessfully added {e.Message.Author.Username} to the list of players");
                    }
                }
                if((e.Message.Content.Contains("Start") || e.Message.Content.Contains("start")) && !theWorld.gameStarted)
                {
                    theWorld.gameStarted = true;
                    string playerString = new string("");
                    foreach(player stringAdd in theWorld.players)
                    {
                        playerString += stringAdd.thePlayer.Username.ToString();
                        playerString += ", ";
                    }
                    e.Message.RespondAsync($"Started game with these players: {playerString}");
                }

            return Task.CompletedTask;
        }
        private Task OnClientReady(ReadyEventArgs e)
        {
            return Task.CompletedTask;
        }
    }
    class world
    {
        public List<player> players { get; set; }
        public List<planet> planets { get; set; }
        public bool gameStarted { get; set; }


        // ================ Commands
        // Most of these methods pull the requisit planet and player information via strings within the objects.
        // For example the current location in player is stored as a string instead of a planet object. We iterate over the entire planet list until we find a name that maches  the string currLocation
        public async Task Status(MessageCreateEventArgs e) //Current status of the player and their ship
        {
            if (e.Message.Content.ToLower() == "status")
            {
                foreach (player questionPlayer in players)
                {
                    if (questionPlayer.thePlayer == e.Message.Author) //This is the player asking the status update
                    {
                        if (questionPlayer.travelTimeLeft < 1)
                        {
                            await e.Message.RespondAsync($"You are currently on {questionPlayer.currLocation.planetName} its catalog is as follows:");
                            string planetOptionsString = "\n /=====================================\\";
                            planetOptionsString += questionPlayer.currLocation.ToString();
                            await e.Message.RespondAsync(planetOptionsString + "\n\\=====================================/");
                        }
                        else
                        {
                            await e.Message.RespondAsync($"You are currently traveling to {questionPlayer.destination.planetName} and have {((double)questionPlayer.travelTimeLeft / 60).ToString("N2")} more minutes remaining in route");
                        }
                            await e.Message.RespondAsync($"Your ship moves at a speed of {questionPlayer.shipSpeed} parsecs per hour and has {questionPlayer.storage} units of storage space remaining");
                            await e.Message.RespondAsync($"Your stash of wealth includes {questionPlayer.shmeckles} shmeckles");
                            await e.Message.RespondAsync($"This is the contents of your cargo hold:");
                            foreach (playerItem playerItem in questionPlayer.playerItems)
                            {
                                if (!(playerItem.quantityLeft == 0))
                                    await e.Message.RespondAsync($"{playerItem.quantityLeft} units of {playerItem.itemName}");
                            }
                    }
                }
            }
        }
        public async Task Options(MessageCreateEventArgs e) //Options the questioning player can take at the given moment of time
        {
            if (e.Message.Content.ToLower() == "options")
            {
                foreach (player questionPlayer in players)
                {
                    if (questionPlayer.thePlayer == e.Message.Author) //This is the player that wants their options
                    {
                        if(!(questionPlayer.travelTimeLeft > 1))
                        {
                            await e.Message.RespondAsync($"Your are currently sitting on {questionPlayer.currLocation.planetName} and are ready to *buy* or *sell*. Otherwise you can *travel* to these other locations");
                            string planetOptionsString = "\n /=====================================\\";
                            foreach (planet distToPlanet in planets)
                            {
                                planetOptionsString += $"\n**{distToPlanet.planetName}** can be traveled to in *{(distToPlanet.getDistance(questionPlayer.currLocation, questionPlayer.shipSpeed) * 60).ToString("N2")}* minutes";
                            }
                            await e.Message.RespondAsync(planetOptionsString + "\n\\=====================================/");
                        }
                        else
                        {
                            await e.Message.RespondAsync($"Your are travling through the endless guantlet of space toward {questionPlayer.destination.planetName} so your options are limited. Wait {((double)questionPlayer.travelTimeLeft/60).ToString("N2")} more minutes");
                        }
                    }
                }
            }
        }
        public async Task Buy(MessageCreateEventArgs e)
        {
            if(e.Message.Content.ToLower().StartsWith("buy"))
            {
                string itemToBuy = getItemFromString(e.Message.Content);
                string convertToNumber = e.Message.Content;
                string numberOnly = Regex.Replace(e.Message.Content, "[^0-9.]", "");
                int quantityToBuy = Convert.ToInt32(numberOnly);
                if (itemToBuy == "")
                {
                    await e.Message.RespondAsync("Couldnt find a valid item in this buy request. Review the planet catalog by typing 'status'. Proper format is buy [quantity] [item]");
                    return;
                }
                foreach (player player in players)
                {
                    if(player.thePlayer == e.Message.Author) //This is the player trying to sell something
                    {
                        await player.Buy(itemToBuy, quantityToBuy, e);
                    }
                }
            }
        }
        public async Task Sell(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().StartsWith("sell"))
            {
                if (e.Message.Content.ToLower().Contains("upgrade"))
                {
                    await e.Message.RespondAsync("Sorry, no upgrade refunds");
                    return;
                }
                string itemToSell = getItemFromString(e.Message.Content);
                string convertToNumber = e.Message.Content;
                string numberOnly = Regex.Replace(e.Message.Content, "[^0-9.]", "");
                int quantityToSell = Convert.ToInt32(numberOnly);

                if (itemToSell == "")
                {
                    await e.Message.RespondAsync("Couldnt find a valid item in this sell request. Review the planet catalog by typing 'status'. Proper format is sell [Quantity] [item]");
                    return;
                }

                foreach (player player in players)
                    if (player.thePlayer == e.Message.Author) //This is the player trying to buy something
                                await player.Sell(itemToSell, quantityToSell, e);
            }
        }
        public async Task TravelTo(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().StartsWith("travel") || e.Message.Content.ToLower().StartsWith("move"))
            {
                string planetName = getPlanetFromString(e.Message.Content);
                if (planetName == "Marketplace")
                {
                    await e.Message.RespondAsync("You cant go to marketplace yet ):<");
                    return;
                }
                if (planetName == "")
                {
                    await e.Message.RespondAsync("Couldnt find a valid planet in this travel request. Review planets you can travel to with 'options'");
                    return;
                }
                foreach (player player in players)
                    if (player.thePlayer == e.Message.Author) // This is the player trying to travel
                        foreach (planet destination in planets)
                            if (destination.planetName.ToLower() == planetName.ToLower()) // This is the planet the player is trying to go to
                            {
                                if (player.travelTimeLeft > 1)
                                {
                                    await e.Message.RespondAsync($"You are already enroute to {player.destination.planetName} please wait {player.travelTimeLeft / 60} more minutes");
                                }
                                else
                                {
                                    await player.travelTo(destination, e);
                                    await e.Message.RespondAsync($"Confirmed {player.thePlayer.Username}! you're on your way to {player.destination.planetName} see you in {((double)player.travelTimeLeft / 60).ToString("N2")} minutes");
                                }
                            }
            }
        }
        public async Task Map(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().StartsWith("map"))
            {
                foreach (player thePlayer in players)
                {
                    if (e.Message.Author == thePlayer.thePlayer)
                    {
                        await e.Message.RespondAsync($"{thePlayer.thePlayer.Username} here is your map");
                        if (thePlayer.currLocation.planetName.ToLower().Contains("food"))
                            await e.Message.RespondWithFileAsync("inFood.png");
                        else if (thePlayer.currLocation.planetName.ToLower().Contains("market"))
                            await e.Message.RespondWithFileAsync("inMarket.png");
                        else if (thePlayer.currLocation.planetName.ToLower().Contains("circuit"))
                        {
                            await e.Message.RespondWithFileAsync("inCircuit.png");
                        }
                        else if (thePlayer.currLocation.planetName.ToLower().Contains("orion"))
                            await e.Message.RespondWithFileAsync("inOrion.png");
                        else if (thePlayer.currLocation.planetName.ToLower().Contains("spite"))
                            await e.Message.RespondWithFileAsync("inSpite.png");
                        else if (thePlayer.currLocation.planetName.ToLower().Contains("gprofess"))
                            await e.Message.RespondWithFileAsync("inG.png");
                        else if (thePlayer.currLocation.planetName.ToLower().Contains("water"))
                            await e.Message.RespondWithFileAsync("inWater.png");
                        else
                            await e.Message.RespondWithFileAsync("defaultMap.png");
                    }

                }
            }
        }
        public async Task Upgrade(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().StartsWith("upgrade"))
            {
                if (e.Message.Content.ToLower().Contains("sell"))
                {
                    await e.Message.RespondAsync("Sorry, no upgrade refunds");
                    return;
                }
                bool upgradeFlag = false;
                foreach (player player in players)
                    if (player.thePlayer == e.Message.Author)
                        if (player.travelTimeLeft <= 0)
                        {
                            if (player.destination.planetName.ToLower() == "circuit city")
                            {
                                upgradeFlag = true;
                                if (e.Message.Content.ToLower().Contains("info"))
                                    await e.Message.RespondAsync($"Circuit City can upgrade your puny {player.shipSpeed} parsecs an hour engine to a fancy {player.shipSpeed + 2} parsecs a turn engine for the low price of *{(player.shipSpeed + 2) * 500}* shmeckles.ʷᵒʷᵎ");
                                else if (e.Message.Content.ToLower().Contains("buy"))
                                {
                                    if (player.shmeckles < ((player.shipSpeed + 2) * 500))
                                        await e.Message.RespondAsync($"Sorry no can do. You're short nearly {((player.shipSpeed + 2) * 500) - player.shmeckles} shmeckles");
                                    else
                                    {
                                        player.shmeckles -= ((player.shipSpeed + 2) * 500);
                                        player.shipSpeed += 2;
                                        await e.Message.RespondAsync($"Congrdulations you're now the proud owner of a {player.shipSpeed} parsecs per turn engine! {((player.shipSpeed) * 500)} shmeckles have been debited from your account ᵗᵉʳᵐˢ ᵃⁿᵈ ᶜᵒⁿᵈᶦᵗᶦᵒⁿˢ ᵐᵃʸ ᵃᵖᵖˡʸ. ᴬˡˡ ᵖᵘʳᶜʰᵃˢᵉˢ ᶠʳᵒᵐ ᶜᶦʳᶜᵘᶦᵗ ᶜᶦᵗʸ™ ᵃʳᵉ ⁿᵒⁿ ʳᵉᶠᵘⁿᵈᵃᵇˡᵉ");
                                    }
                                }
                                else if (e.Message.Content.ToLower() == "upgrade")
                                {
                                    await e.Message.RespondAsync($"For Upgrade information type 'upgrade info'. To make a purcahse type 'upgrade buy'");
                                }
                            }
                            if (player.destination.planetName.ToLower() == "spite world")
                            {
                                upgradeFlag = true;
                                if (e.Message.Content.ToLower().Contains("info"))
                                    await e.Message.RespondAsync($"Spite World can upgrade your abismal {player.storageTotal} unit storage container to a vast {player.storageTotal + 50} unit storage container for the reasonable price of *{(player.storageTotal + 50) * 100}* shmeckles.ʷᵒʷᵎ");
                                else if (e.Message.Content.ToLower().Contains("buy"))
                                {
                                    if (player.shmeckles < ((player.storageTotal + 50) * 100))
                                        await e.Message.RespondAsync($"Sorry no can do. You're short nearly {((player.shipSpeed + 2) * 500) - player.shmeckles} shmeckles");
                                    else
                                    {
                                        player.shmeckles -= ((player.storageTotal + 50) * 100);
                                        player.storageTotal += 50;
                                        player.storage += 50;
                                        await e.Message.RespondAsync($"Congrdulations you're now the proud owner of a {player.storageTotal} unit storage container! {((player.storageTotal) * 100)} shmeckles have been debited from your account ᵂᵃʳⁿᶦⁿᵍ: ᴿᵒᶜᵏᵛᶦˡˡᵉ ᶦˢ ⁿᵒᵗ ʳᵉˢᵖᵒⁿˢᶦᵇˡᵉ ᶠᵒʳ ᵃⁿʸ ᵈᵃᵐᵃᵍᵉᵈ ᵖʳᵒᵈᵘᶜᵗ ᶦⁿᵃᵈᵛᵉʳᵗᵉⁿᵗˡʸ ᵉˣᵖᵒˢᵉᵈ ᵗᵒ ᵛᵃᶜᵘᵘᵐ");
                                    }
                                }
                                else if (e.Message.Content.ToLower() == "upgrade")
                                {
                                    await e.Message.RespondAsync($"For Upgrade information, type 'upgrade info'. To make a purcahse, type 'upgrade buy'");
                                }
                            }

                            if (!upgradeFlag)
                                await e.Message.RespondAsync($"There are no available upgrades for this planet (yet). or you're in space, this is temporary anyways go away");
                        }
            }
        }
        public async Task Help(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().StartsWith("help"))
                await e.Message.RespondAsync($"Bazillionaire (this game) is an economic simulation game.\n " +
                    $"There are 8 planets and 8 products to trade. Its up to you to move product between planets to turn a profit. Some planets want specific product more than others and are willing to pay a high price for them.\n" +
                    $"You start with a measly 3 parsec a turn starship with 200 units of storage space. There are several commands to help you accomplish your tasks.\n" +
                    $"**Status** -- Gives the status of your ship, money and the catalog of the current planet you are on\n" +
                    $"**Options** -- Tells you what options are available to you at this given moment of time\n" +
                    $"**Buy** *quantity* *item* -- Buys a certain amount of an item from the local planets at its current local value (Check the catalog with 'status'!)\n" +
                    $"**Sell** *quantity* *item* -- Sells a certain amount of an item to the local planet at the current local value\n" +
                    $"**Upgrade** info -- gives information on what upgrades the current planet offers for your ship\n" +
                    $"**Upgrade** buy -- Purchases the upgrade your current planet offers at their asking price\n" +
                    $"**Help** -- Pulls this screen back up\n" +
                    $"**Map** -- Pulls up a map of the star system and your current location");
        }

        private string getItemFromString(string theString)
        {
            string testString = theString.ToLower();
            if (testString.Contains("food"))
                return "Food";
            if (testString.Contains("spice"))
                return "Spice";
            if (testString.Contains("water"))
                return "water";
            if (testString.Contains("circuits"))
                return "Circuits";
            if (testString.Contains("rocks"))
                return "Rocks";
            if (testString.Contains("oil"))
                return "Oil";
            if (testString.Contains("anime figurines"))
                return "Anime Figurines";
            if (testString.Contains("space aoemebas"))
                return "Space Aoemebas";
            else
            {
                return "";
            }
        }
        private string getPlanetFromString(string theString)
        {
            if (theString.ToLower().Contains("rockville"))
                return "Rockville";
            if (theString.ToLower().Contains("orion"))
                return "Orion";
            if (theString.ToLower().Contains("water"))
                return "Water Planet";
            if (theString.ToLower().Contains("spite"))
                return "Spite World";
            if (theString.ToLower().Contains("circuit"))
                return "Circuit City";
            if (theString.ToLower().Contains("food"))
                return "Food Land";
            if (theString.ToLower().Contains("america"))
                return "America Town";
            if (theString.ToLower().Contains("gprofessionalweebville"))
                return "Gprofessionalweebville";
            if (theString.ToLower().Contains("marketplace"))
                return "Marketplace";
            else
            {
                return "";
            }

        }
        public void tickWorld()
        {
            foreach(planet tickPlanet in planets)
            {
                tickPlanet.tickPlanet(secondsPerTick);
            }
            foreach(player player in players)
            {
                player.travelTick(secondsPerTick);
            }
        }
        public const int secondsPerTick = 10;
        public world()
        {
            gameStarted = false;
            players = new List<player>();
            planets = new List<planet>();
            planets.Add(new planet("Gprofessionalweebville", 1));
            planets.Add(new planet("Rockville", 1));
            planets.Add(new planet("America Town", 1));
            planets.Add(new planet("Food Land", 1));
            planets.Add(new planet("Circuit City", 1));
            planets.Add(new planet("Spiteworld", 1));
            planets.Add(new planet("Water Planet", 1));
            planets.Add(new planet("Orion", 1));
        }
    };
    class player
    {
        public player(DiscordUser thePlayer, planet startLocation)
        {
            this.thePlayer = thePlayer;
            shmeckles = 5000;
            storage = 200;//Units of stuff
            playerItems = new List<playerItem>();
            travelTimeLeft = 0;
            shipSpeed = 3;

            playerItems.Add(new playerItem("Food"));
            playerItems.Add(new playerItem("Spice"));
            playerItems.Add(new playerItem("Water"));
            playerItems.Add(new playerItem("Circuits"));
            playerItems.Add(new playerItem("Rocks"));
            playerItems.Add(new playerItem("Oil"));
            playerItems.Add(new playerItem("Anime Figurines"));
            playerItems.Add(new playerItem("Space Aoemebas"));

            currLocation = startLocation;
            destination = startLocation;
            storageTotal = storage;
        }
        public DiscordUser thePlayer { get; set; }
        public List<playerItem> playerItems;
        public int shmeckles { get; set; }
        public int storage { get; set; }
        public int storageTotal { get; set; }
        public planet currLocation { get; set; }
        public planet destination { get; set; }
        public int travelTimeLeft { get; set; } //In seconds
        public int shipSpeed { get; set; } //Parsecs per hour

        public async Task Buy(string item, int quantity, MessageCreateEventArgs e)
        {
            if (travelTimeLeft > 0)
            {
                await e.Message.RespondAsync($"Your in space fool, you cant buy anything");
                return;
            }
            //Make sure we're not going over storage limits
            int totalStorageUsed = 0;
            foreach (playerItem playerItem in playerItems)
                totalStorageUsed += playerItem.quantityLeft;
            if (totalStorageUsed + quantity > storage)
            {
                await e.Message.RespondAsync($"Not enough storage space left cant buy that much!");
                return;
            }

            foreach (planetItem planetItem in currLocation.items)
            {
                if(item.ToLower() == planetItem.itemName.ToLower())
                {
                    if (quantity > planetItem.quantityLeft)
                        await e.Message.RespondAsync($"You cant buy what the planet doesnt have enough of ):<");
                    else if (quantity * planetItem.pricePerUnit > shmeckles)
                        await e.Message.RespondAsync($"Sorry {e.Message.Author.Username}, I cant give credit. Come back when you're a little MMMMMMMMMMMMMMMMMMM richer!");
                    else // Nothing stopping us from making this purchase
                    {

                        foreach (playerItem playerItem in playerItems)
                        {
                            if (playerItem.itemName.ToLower() == item.ToLower()) //This is the item we're trying to manipulate
                            {
                                playerItem.quantityLeft += quantity;
                                shmeckles -= (int)Math.Round(quantity * planetItem.pricePerUnit);

                                storage -= quantity;

                                planetItem.quantityLeft -= quantity;
                                await e.Message.RespondAsync($"Sucessfully bought {quantity} {item}s for {(int)Math.Round(quantity * planetItem.pricePerUnit)} shmeckles.\n You now have {shmeckles} shmeckles!");
                            }
                        }
                    }
                }
            }
            return;
        }
        public async Task Sell(string item, int quantity, MessageCreateEventArgs e)
        {
            if (travelTimeLeft > 0)
            {
                await e.Message.RespondAsync($"Your in space fool, you cant sell anything");
                return;
            }
            foreach (planetItem planetItem in currLocation.items)
            {
                if(planetItem.itemName.ToLower() == item.ToLower()) //This is the item we want to manipulate
                {
                    foreach(playerItem playerItem in playerItems)
                    {
                        if (playerItem.itemName.ToLower() == item.ToLower()) //This is the item we're trying to manipulate
                        {
                            if (playerItem.quantityLeft < quantity)
                            {
                                await e.Message.RespondAsync($"You cant sell what you dont have :|");
                            }
                            else
                            {
                                playerItem.quantityLeft -= quantity;
                                shmeckles += (int)Math.Round(quantity * planetItem.pricePerUnit);

                                storage += quantity;

                                planetItem.quantityLeft += quantity;
                                await e.Message.RespondAsync($"Sucessfully sold {quantity} {item}s for {(int)Math.Round(quantity * planetItem.pricePerUnit)} shmeckles.\n You now have {shmeckles} shmeckles!");
                            }
                        }
                    }
                }
            }
            return;
        }
        public void travelTick(int secondsPerTick)
        {
            travelTimeLeft -= secondsPerTick;
            if (travelTimeLeft < 0)
                currLocation = destination;
        }
        public async Task travelTo(planet destination, MessageCreateEventArgs e)
        {
            if (!(travelTimeLeft > 0))
            {
                this.destination = destination;
                double tempTravelTime = ((double)currLocation.getDistance(destination, shipSpeed));
                travelTimeLeft = 3600;
                tempTravelTime = ((double)travelTimeLeft) * tempTravelTime;
                travelTimeLeft = (int)tempTravelTime;
            }
        }
    };
    class playerItem
    {
        public playerItem(string itemName)
        {
            this.itemName = itemName;
            quantityLeft = 0;
        }
        public string itemName { get; private set; }
        public int quantityLeft { get; set; }
    }
    class planet
    {
        public planet(string planetName, int distanceMultiplier) //Planet name determines location and item setups
        {
            // Notes. 20/hrs production should be the planets main production. Secondary productions should be <10. consumptions should be >-10 unless there is a good reason for it
            // TODO: make most of the production values non arbitrary :p
            if (planetName.ToLower() == "rockville")
            {
                this.planetName = planetName;
                xLoc = -1 * distanceMultiplier;
                yLoc = 1 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, -8, 200));
                items.Add(new planetItem("Spice", 100, 100, -5, 200));
                items.Add(new planetItem("Water", 100, 100, -12, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, -1, 200));
                items.Add(new planetItem("Rocks", 100, 100, 20, 200));
                items.Add(new planetItem("Oil", 100, 100, -1, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 100, 500, 0, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 200, -1, 200));
            }
            else if (planetName.ToLower() == "spiteworld")
            {
                this.planetName = planetName;
                xLoc = 1 * distanceMultiplier;
                yLoc = 0 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, -8, 200));
                items.Add(new planetItem("Spice", 100, 100, 15, 200));
                items.Add(new planetItem("Water", 100, 100, -12, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, -4, 200));
                items.Add(new planetItem("Rocks", 100, 100, 0, 200));
                items.Add(new planetItem("Oil", 100, 100, -2, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 100, 200, -4, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 300, -1, 200));
            }
            else if (planetName.ToLower() == "circuit city")
            {
                this.planetName = planetName;
                xLoc = 1 * distanceMultiplier;
                yLoc = -1 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, -8, 200));
                items.Add(new planetItem("Spice", 100, 100, -5, 200));
                items.Add(new planetItem("Water", 100, 100, -12, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, 16, 200));
                items.Add(new planetItem("Rocks", 100, 100, 0, 200));
                items.Add(new planetItem("Oil", 100, 100, -2, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 100, 400, -4, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 300, -1, 200));
            }
            else if (planetName.ToLower() == "food land")
            {
                this.planetName = planetName;
                xLoc = -1 * distanceMultiplier;
                yLoc = -1 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, 12, 200));
                items.Add(new planetItem("Spice", 100, 100, -5, 200));
                items.Add(new planetItem("Water", 100, 100, -6, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, -3, 200));
                items.Add(new planetItem("Rocks", 100, 100, -1, 200));
                items.Add(new planetItem("Oil", 100, 100, 4, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 100, 200, -4, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 600, -1, 200));
            }
            else if (planetName.ToLower() == "america town")
            {
                this.planetName = planetName;
                xLoc = -1 * distanceMultiplier;
                yLoc = 0 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, -8, 200));
                items.Add(new planetItem("Spice", 100, 100, -5, 200));
                items.Add(new planetItem("Water", 100, 100, -12, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, -3, 200));
                items.Add(new planetItem("Rocks", 100, 100, -9, 200));
                items.Add(new planetItem("Oil", 500, 500, 27, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 150, 400, -4, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 300, -1, 200));
            }
            else if (planetName.ToLower() == "gprofessionalweebville")
            {
                this.planetName = planetName;
                xLoc = 0 * distanceMultiplier;
                yLoc = 0 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, -12, 200));
                items.Add(new planetItem("Spice", 100, 100, -7, 200));
                items.Add(new planetItem("Water", 100, 100, -14, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, -3, 200));
                items.Add(new planetItem("Rocks", 100, 100, 9, 200));
                items.Add(new planetItem("Oil", 500, 500, -8, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 100, 200, 14, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 300, 2, 200));
            }
            else if (planetName.ToLower() == "orion")
            {
                this.planetName = planetName;
                xLoc = 0 * distanceMultiplier;
                yLoc = 2 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, 4, 200));
                items.Add(new planetItem("Spice", 100, 100, 3, 200));
                items.Add(new planetItem("Water", 100, 100, 6, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, -3, 200));
                items.Add(new planetItem("Rocks", 100, 100, -9, 200));
                items.Add(new planetItem("Oil", 500, 500, -8, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 100, 200, 2, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 300, 21, 200));
            }
            else if (planetName.ToLower() == "water planet")
            {
                this.planetName = planetName;
                xLoc = 1 * distanceMultiplier;
                yLoc = 1 * distanceMultiplier;
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", 100, 100, 4, 200));
                items.Add(new planetItem("Spice", 100, 100, -1, 200));
                items.Add(new planetItem("Water", 100, 100, 30, 200));


                //Production Stuff
                items.Add(new planetItem("Circuits", 100, 100, -3, 200));
                items.Add(new planetItem("Rocks", 100, 100, -9, 200));
                items.Add(new planetItem("Oil", 500, 500, -8, 200));

                //Exotic Stuff
                items.Add(new planetItem("Anime Figurines", 100, 200, 2, 200));
                items.Add(new planetItem("Space Aoemebas", 100, 300, 21, 200));
            }
            else
                throw new System.ArgumentException($"{planetName} is not a valid planet name :|");
        }
        public void tickPlanet(int secondsPerTick)
        {
            foreach(planetItem item in items)
            {
                item.produce(secondsPerTick);
                item.skewPrice(secondsPerTick);
            }
        }
        public double getDistance(planet planetRef, int speed) //Returns the time it would take to travel between two planets
        {
            double distanceBetweenPlanets = 3;
            if (planetRef.xLoc > xLoc && planetRef.yLoc > yLoc)
                distanceBetweenPlanets = Math.Sqrt(Math.Pow(planetRef.xLoc - xLoc, 2) + Math.Pow(planetRef.yLoc - yLoc, 2));
            else if (planetRef.xLoc <= xLoc && planetRef.yLoc > yLoc)
                distanceBetweenPlanets = Math.Sqrt(Math.Pow(xLoc - planetRef.xLoc, 2) + Math.Pow(planetRef.yLoc - yLoc, 2));
            else if (planetRef.xLoc > xLoc && planetRef.yLoc <= yLoc)
                distanceBetweenPlanets = Math.Sqrt(Math.Pow(planetRef.xLoc - xLoc, 2) + Math.Pow(yLoc - planetRef.yLoc, 2));
            else if (planetRef.xLoc <= xLoc && planetRef.yLoc <= yLoc)
                distanceBetweenPlanets = Math.Sqrt(Math.Pow(xLoc - planetRef.xLoc, 2) + Math.Pow(yLoc - planetRef.yLoc, 2));

            return distanceBetweenPlanets / speed;
        }
        public override string ToString()
        {
            string retString = "";
            foreach(planetItem item in items)
            {
                retString += $"\n_{item.quantityLeft}_ units of **{item.itemName}** at a price of *{(item.pricePerUnit).ToString("N2")} shmeckles*";
            }
            return retString;
        }
        public string planetName;
        public List<planetItem> items;

        private int xLoc; // Measured in parsecs. Obviously
        private int yLoc;

    };
    class planetItem
    {
        public planetItem(string itemName, int initQuantity, int quantityExpected, double productionRate, int basePrice)
        {
            this.itemName = itemName;
            quantityLeft = initQuantity;
            this.productionRate = productionRate;
            progressToNextItem = 0.0;

            pricePerUnit = 100;
            this.quantityExpected = quantityExpected;

            this.basePrice = basePrice;
        }
        public string itemName { get; private set; }
        public int quantityLeft { get; set; }
        private double productionRate; //How much of this produced per hour. Negative values mean it is consumed
        private double progressToNextItem; //Any number above 1 will be removed at the next production step and added to quantityLeft

        public double pricePerUnit { get; private set; }
        private int quantityExpected; //If quantityLeft < quantityExpected then the price goes up and vice versa

        private int basePrice;

        public void skewPrice(int secondsPerTick) //The higher the surplus or shortage the more the price will change
        {
            double targetPrice = 0;

            targetPrice = (-(Math.Log(quantityLeft/quantityExpected)))/(basePrice * .02);
            if (targetPrice < (double)(basePrice - basePrice * .25))
                targetPrice = basePrice - basePrice * .25;
            if (targetPrice > (double)(basePrice + basePrice * .25))
                targetPrice = (basePrice + basePrice * .25);


                double maxAdd = ((double)20 / 3600 * secondsPerTick);
                if (targetPrice > pricePerUnit)
                {
                    if (targetPrice - maxAdd > pricePerUnit) // targetPrice is MUCH higher. Just add the maximum amount
                    {
                        pricePerUnit += maxAdd;
                    }
                    else //Not higher than maxAdd. Make them equal
                        pricePerUnit = targetPrice;
                }
                else
                {
                    if (targetPrice + maxAdd < pricePerUnit) // targetPrice is MUCH lower. Just subtract the maximum amount
                        pricePerUnit -= maxAdd;
                    else //Not lower than maxAdd. Make them equal
                        pricePerUnit = targetPrice;
                }
            }
        public void produce(int secondsPerTick) //Add the production per hour to the quantityleft
        {
            progressToNextItem += productionRate / 3600 * secondsPerTick;
            while (progressToNextItem >= 1)
            {
                progressToNextItem--;
                quantityLeft += 1;
            }
            while (progressToNextItem <= -1)
            {
                progressToNextItem++;
                quantityLeft -= 1;
            }
            if (quantityLeft < 0)
                quantityLeft = 0;
        }
    }
}
