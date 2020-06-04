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
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
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
            Client.MessageCreated += theWorld.SaleConfirmation;
            Client.MessageCreated += theWorld.Status;
            Client.MessageCreated += theWorld.Options;
            Client.MessageCreated += theWorld.Sell;
            Client.MessageCreated += theWorld.Buy;
            Client.MessageCreated += theWorld.TravelTo;
            Client.MessageCreated += theWorld.Upgrade;
            Client.MessageCreated += theWorld.Map;
            Client.MessageCreated += theWorld.Help;
            Client.MessageCreated += theWorld.Cheat;
            Client.MessageCreated += theWorld.Catalog;
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
                        theWorld.players.Add(new player(e.Message.Author, theWorld.planets[4], e.Message.Channel));
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
            if (e.Message.Content.ToLower().Contains("status"))
            {
                foreach (player questionPlayer in players)
                {
                    if (questionPlayer.thePlayer == e.Message.Author) //This is the player asking the status update
                    {
                        string responseString = "";
                        if (questionPlayer.travelTimeLeft < 1)
                        {
                            await e.Message.RespondAsync($"You are currently on {questionPlayer.currLocation.planetName} its catalog is as follows:");
                            string planetOptionsString = "\n /=====================================\\";
                            planetOptionsString += questionPlayer.currLocation.ToString();
                            await e.Message.RespondAsync(planetOptionsString + "\n\\=====================================/");
                        }
                        else
                        {
                            responseString += $"You are currently traveling to {questionPlayer.destination.planetName} and have {((double)questionPlayer.travelTimeLeft / 60).ToString("N2")} more minutes remaining in route\n";
                        }
                            responseString += $"Your ship moves at a speed of {questionPlayer.shipSpeed} parsecs per hour and has {questionPlayer.storage} units of storage space remaining\n";
                            responseString += $"Your stash of wealth includes {questionPlayer.shmeckles} shmeckles\n";
                            responseString += $"This is the contents of your cargo hold:\n";
                            foreach (playerItem playerItem in questionPlayer.playerItems)
                            {
                                if (!(playerItem.quantityLeft == 0))
                                    responseString += ($"{playerItem.quantityLeft} units of {playerItem.itemName}");
                            }
                        await e.Message.RespondAsync(responseString);
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
                                planetOptionsString += $"\n**{distToPlanet.planetName}** can be traveled to in *{(distToPlanet.getDistance(questionPlayer.xLoc, questionPlayer.yLoc) / questionPlayer.shipSpeed * 60).ToString("N2")}* minutes";
                            }
                            await e.Message.RespondAsync(planetOptionsString + "\n\\=====================================/");
                        }
                        else
                        {
                            string planetOptionsString = "\n /=====================================\\";
                            foreach (planet distToPlanet in planets)
                            {
                                planetOptionsString += $"\n**{distToPlanet.planetName}** can be traveled to in *{(distToPlanet.getDistance(questionPlayer.xLoc, questionPlayer.yLoc) / questionPlayer.shipSpeed * 60).ToString("N2")}* minutes";
                            }
                            await e.Message.RespondAsync($"Your are travling through the endless guantlet of space toward {questionPlayer.destination.planetName}. Wait {((double)questionPlayer.travelTimeLeft/60).ToString("N2")} more minutes\n" +
                                $"Alternatively you can change your route to these destinations: " + planetOptionsString + "\n\\=====================================/");
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
                    if(player.thePlayer == e.Message.Author) //This is the player trying to buy something
                    {
                        player.nextSaleItem = itemToBuy;
                        player.nextSaleQuantity = quantityToBuy;
                        player.buying = true;
                        foreach (planetItem pItem in player.currLocation.items)
                        {
                            if (pItem.itemName == itemToBuy)
                            {
                                if (e.Message.Content.ToLower().Contains("max") || quantityToBuy > 2000 || quantityToBuy < 0)
                                {
                                    quantityToBuy = (int)(player.shmeckles / pItem.pricePerUnit);
                                }
                                if (pItem.itemName == itemToBuy)
                                    await e.Message.RespondAsync(pItem.getRandomFlavorText());
                                await e.Message.RespondAsync($"Confirm buy ({quantityToBuy} {itemToBuy} @ {pItem.pricePerUnit.ToString("N2")} shmeckles)? (y/n)");
                            }
                        }
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
                    {
                        foreach(planetItem pItem in player.currLocation.items)
                        {
                            if (pItem.itemName == itemToSell)
                            {
                                if (e.Message.Content.ToLower().Contains("max") || quantityToSell > 2000 || quantityToSell < 0)
                                {
                                    quantityToSell = (int)(player.shmeckles / pItem.pricePerUnit);
                                }
                                player.nextSaleItem = itemToSell;
                                player.nextSaleQuantity = quantityToSell;
                                player.selling = true;
                                await e.Message.RespondAsync($"Confirm buy ({quantityToSell} {itemToSell} @ {pItem.pricePerUnit.ToString("N2")} shmeckles)? (y/n)");
                            }
                        }
                    }
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
                                    await player.travelTo(destination, e);
                                    await e.Message.RespondAsync($"Confirmed {player.thePlayer.Username}! you're on your way to {player.destination.planetName} see you in {(player.travelTimeLeft / 60).ToString("N2")} minutes");
                            }
            }
        }
        public async Task Map(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().StartsWith("map"))
                foreach (player thePlayer in players)
                    if (e.Message.Author == thePlayer.thePlayer)
                    {
                        //Setup basic stuff
                        System.Drawing.RectangleF destinationRectangle;
                        Image newImage = new Bitmap(1103, 640);
                        Graphics newCanvas = Graphics.FromImage(newImage);

                        string[] planetFiles = Directory.GetFiles("Images/Planets/");
                        GraphicsUnit units = GraphicsUnit.Pixel;

                        int sizeX = 960;
                        int sizeY = 720;
                        RectangleF sourceRectangle = new RectangleF(0, 0, sizeX, sizeY);
                        
                        //Draw bkacround
                        destinationRectangle = new RectangleF(0, 0, 1103, 640);
                        Image tempImage = Image.FromFile("Images/Blank.jpg");
                        newCanvas.DrawImage(tempImage, destinationRectangle, destinationRectangle, units);
                        tempImage.Dispose();

                        //Draw planets
                        foreach (string planetFile in planetFiles)
                        {
                            string thePlanet = getPlanetFromString(planetFile);
                            foreach (planet planet in planets)
                            {
                                if (planet.planetName == thePlanet)
                                {
                                    destinationRectangle = new RectangleF((float)(planet.xLoc + sizeX / 2), (float)(planet.yLoc + sizeY / 2), sizeX, sizeY);
                                    tempImage = Image.FromFile(planetFile);
                                    newCanvas.DrawImage(tempImage, destinationRectangle, sourceRectangle, units);
                                    tempImage.Dispose();
                                }
                            }
                        }
                        //Print to discord!
                        newImage.Save("Images/Map/currentMap.jpg");
                        await e.Message.RespondWithFileAsync("Images/Map/currentMap.jpg");
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
                            if (player.destination.planetName.ToLower() == "persepolis")
                            {
                                upgradeFlag = true;
                                if (e.Message.Content.ToLower().Contains("info"))
                                    await e.Message.RespondAsync($"Persepolis can upgrade your puny {player.shipSpeed} parsecs an hour engine to a fancy {player.shipSpeed + 2} parsecs a turn engine for the low price of *{(player.shipSpeed + 2) * 500}* shmeckles.ʷᵒʷᵎ");
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
                            if (player.destination.planetName.ToLower() == "arrakis")
                            {
                                upgradeFlag = true;
                                if (e.Message.Content.ToLower().Contains("info"))
                                    await e.Message.RespondAsync($"Arrakis can upgrade your abismal {player.storageTotal} unit storage container to a vast {player.storageTotal + 50} unit storage container for the reasonable price of *{(player.storageTotal + 50) * 100}* shmeckles.ʷᵒʷᵎ");
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
                            if (player.destination.planetName.ToLower().Contains("shin"))
                            {
                                upgradeFlag = true;
                                if (e.Message.Content.ToLower().Contains("info"))
                                {
                                    await e.Message.RespondAsync($"Shin-Akihabara can give you permanent vision on a specific item for a specific planet. The more you upgrade the vision the more information you can see. Format is upgrade [planet name] [item]. Cost is 500 shmeckles an upgrade");
                                    return;
                                }
                                string upgradeItem = getItemFromString(e.Message.Content);
                                string upgradePlanet = getPlanetFromString(e.Message.Content);
                                if (upgradeItem != "")
                                {
                                    if (upgradePlanet != "")
                                    {
                                        if (player.shmeckles > 500)
                                        {
                                            foreach (itemUpgrade upgrade in player.upgrades)
                                            {
                                                if (upgrade.visionPlanet.planetName == upgradePlanet && upgrade.visionItem.itemName == upgradeItem)
                                                {
                                                    if (upgrade.upgradeLevel > 2)
                                                    {
                                                        await e.Message.RespondAsync("You already have the maximum vision of that item at all times.");
                                                        return;
                                                    }
                                                    upgrade.upgradeLevel++;
                                                    player.shmeckles -= 500;
                                                    await e.Message.RespondAsync($"Sucessfully upgraded your vision of {upgradeItem} on {upgradePlanet} to level {upgrade.upgradeLevel} for 500 shmeckles");
                                                    return;
                                                }
                                            }
                                            foreach (planet planet in planets)
                                                foreach(planetItem item in planet.items)
                                                    if(planet.planetName == upgradePlanet && item.itemName == upgradeItem)
                                                    {
                                                        player.upgrades.Add(new itemUpgrade(planet, item));
                                                        await e.Message.RespondAsync($"Sucessfully upgraded your vision of {upgradeItem} on {upgradePlanet} to level 0 for 500 shmeckles");
                                                        return;
                                                    }
                                        }
                                        else
                                            await e.Message.RespondAsync("Not enough money for this upgrade!");
                                    }
                                    else
                                        await e.Message.RespondAsync("Couldnt find a valid planet to get vision on in this upgrade request");
                                }
                                else
                                    await e.Message.RespondAsync("Couldnt find a valid item to upgrade in this upgrade request");
                            }
                            if (!upgradeFlag)
                                await e.Message.RespondAsync($"There are no available upgrades for this planet (yet). or you're in space!");
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
        public async Task Cheat(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().StartsWith("cheat"))
            {
                if (e.Message.Author.Username.ToLower() != "Excellent")
                {
                    foreach (player player in players)
                        if (player.thePlayer == e.Message.Author)
                        {
                            if (e.Message.Content.ToLower().Contains("engine"))
                            {
                                player.shipSpeed = 50;
                                await e.Message.RespondAsync($"Gave player {player.thePlayer.Username} fast as fuck engine");
                            }
                        }
                    if (e.Message.Content.ToLower().Contains("advance"))
                    {
                        await e.Message.RespondAsync($"Advancing time by 480 seconds.. Persepolis's orbital period before is {planets[4].orbitalPeriod} and its position is {planets[4].xLoc}, {planets[4].yLoc}) ");
                        for (int i = 0; i <= 48; i++)
                            tickWorld();
                        await e.Message.RespondAsync($"Advanced time. Persepolis's orbital period is now {planets[4].orbitalPeriod} and its position is {planets[4].xLoc}, {planets[4].yLoc})  ");
                    }
                }
                else
                    await e.Message.RespondAsync($"This incident will be reported, {e.Message.Author.Username}");
            }
        }
        public async Task SaleConfirmation(MessageCreateEventArgs e)
        {
            foreach(player confirmingPlayer in players)
            {
                if(confirmingPlayer.buying || confirmingPlayer.selling)
                {
                    if (e.Message.Author == confirmingPlayer.thePlayer)
                    {
                        if (e.Message.Content.ToLower() != "y" && e.Message.Content.ToLower() != "n")
                        {
                            confirmingPlayer.buying = false;
                            confirmingPlayer.selling = false;
                            Console.WriteLine($"Beeg Problem we just stopped {e.Message.Author.Username}");
                        }
                        else
                        {
                            if (e.Message.Content.ToLower() == "n")
                            {
                                await e.Message.RespondAsync("Confirmed, sale canceled");
                                confirmingPlayer.buying = false;
                                confirmingPlayer.selling = false;
                            }
                            if (e.Message.Content.ToLower() == "y")
                            {
                                Console.WriteLine("Actually Buying");
                                if (confirmingPlayer.buying)
                                    await confirmingPlayer.Buy(e);
                                else
                                    await confirmingPlayer.Sell(e);
                                confirmingPlayer.buying = false;
                                confirmingPlayer.selling = false;
                            }
                        }
                    }
                }
            }
        }
        public async Task Catalog(MessageCreateEventArgs e)
        {
            if (e.Message.Content.ToLower().Contains("catalog"))
            {
                foreach (player questionPlayer in players)
                {
                    if (questionPlayer.thePlayer == e.Message.Author) //This is the player asking for a catalog
                    {
                        if (questionPlayer.travelTimeLeft < 1)
                        {
                            planet getPlanetCatalog = questionPlayer.currLocation;
                            if (getPlanetFromString(e.Message.Content) != "") //We're trying to get the catalog from another planet
                            {
                                foreach (planet planet in planets)
                                {
                                    if (planet.planetName == getPlanetFromString(e.Message.Content)) //This is the planet we're trying to get the catalog from
                                    {
                                        getPlanetCatalog = planet;
                                    }
                                }
                            }
                                await e.Message.RespondAsync($"{getPlanetCatalog.planetName}'s catalog is as follows:");
                                string planetOptionsString = "\n /=====================================\\";
                                planetOptionsString += getPlanetCatalog.ToString(questionPlayer.upgrades, questionPlayer.currLocation);
                                await e.Message.RespondAsync(planetOptionsString + "\n\\=====================================/");
                                return;
                        }
                    }
                }
            }
        }

        private string getItemFromString(string theString)
        {
            string testString = theString.ToLower();
            if (testString.Contains("food"))
                return "Food";
            if (testString.Contains("spice"))
                return "Spice";
            if (testString.Contains("water"))
                return "Water";
            if (testString.Contains("electronic"))
                return "Electronics";
            if (testString.Contains("ore"))
                return "Ore";
            if (testString.Contains("fuel"))
                return "Fuel";
            if (testString.Contains("consumer") || testString.Contains("good"))
                return "Consumer Goods";
            if (testString.Contains("aoemeba"))
                return "Aoemebae";
            else
            {
                return "";
            }
        }
        private string getPlanetFromString(string theString)
        {
            if (theString.ToLower().Contains("medu"))
                return "Medusa";
            if (theString.ToLower().Contains("rock"))
                return "Rockefeller Reach";
            if (theString.ToLower().Contains("arrak"))
                return "Arrakis";
            if (theString.ToLower().Contains("demet"))
                return "Demeter";
            if (theString.ToLower().Contains("persep"))
                return "Persepolis";
            if (theString.ToLower().Contains("shin") || theString.ToLower().Contains("akiha"))
                return "Shin-Akihabara";
            if (theString.ToLower().Contains("delp"))
                return "Delphi";
            if (theString.ToLower().Contains("aquar"))
                return "Aquarion";
            if (theString.ToLower().Contains("marketplace"))
                return "Marketplace";
            else
                return "";

        }
        public void tickWorld()
        {
            foreach(planet tickPlanet in planets)
            {
                if(tickPlanet.GetType().ToString() == "bazillionaire.planet")
                    tickPlanet.tickPlanet(secondsPerTick);
                else
                {
                    moon moonTicker = (moon)tickPlanet;
                    moonTicker.tickPlanet(secondsPerTick);
                }
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
            planets.Add(new planet("Medusa", 1));
            planets.Add(new planet("Rockefeller Reach", 2));
            planets.Add(new planet("Arrakis", 3));
            planets.Add(new moon("Demeter", planets[2]));
            planets.Add(new planet("Persepolis", 5));
            planets.Add(new moon("Shin-Akihabara", planets[4]));
            planets.Add(new planet("Delphi", 7));
            planets.Add(new planet("Aquarion", 8));
        }
    };
    class player
    {
        //=====================================User Information
        public player(DiscordUser thePlayer, planet startLocation, DiscordChannel responseChannel)
        {
            this.thePlayer = thePlayer;
            this.responseChannel = responseChannel;
            shmeckles = 5000;
            storage = 200;//Units of stuff
            playerItems = new List<playerItem>();
            travelTimeLeft = -10;
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

            xLoc = startLocation.xLoc;
            yLoc = startLocation.yLoc;
            storageTotal = storage;

            upgrades = new List<itemUpgrade>();
        }
        public DiscordUser thePlayer { get; set; }
        public List<playerItem> playerItems;
        public int shmeckles { get; set; }
        public int storageTotal { get; set; } //Maximum possible stroage
        public int storage { get; set; } //Currently used storage. Is never > storageTotal
        DiscordChannel responseChannel;
        // ====================================Commerce methods
        public async Task Buy(MessageCreateEventArgs e)
        {
            if (travelTimeLeft > 0)
            {
                await e.Message.RespondAsync($"You're in space fool, you cant buy anything");
                return;
            }
            //Make sure we're not going over storage limits
            int totalStorageUsed = 0;
            foreach (playerItem playerItem in playerItems)
                totalStorageUsed += playerItem.quantityLeft;
            if (totalStorageUsed + nextSaleQuantity > storage)
            {
                await e.Message.RespondAsync($"Not enough storage space left cant buy that much!");
                return;
            }

            foreach (planetItem planetItem in currLocation.items)
            {
                if (nextSaleItem.ToLower() == planetItem.itemName.ToLower())
                {
                    if (nextSaleQuantity > planetItem.quantityLeft)
                        await e.Message.RespondAsync($"You cant buy what the planet doesnt have enough of ):<");
                    else if (nextSaleQuantity * planetItem.pricePerUnit > shmeckles)
                        await e.Message.RespondAsync($"Sorry {e.Message.Author.Username}, I cant give credit. Come back when you're a little MMMMMMMMMMMMMMMMMMM richer!");
                    else // Nothing stopping us from making this purchase
                    {

                        foreach (playerItem playerItem in playerItems)
                        {
                            if (playerItem.itemName.ToLower() == nextSaleItem.ToLower()) //This is the item we're trying to manipulate
                            {
                                playerItem.quantityLeft += nextSaleQuantity;
                                shmeckles -= (int)Math.Round(nextSaleQuantity * planetItem.pricePerUnit);

                                storage -= nextSaleQuantity;

                                planetItem.quantityLeft -= nextSaleQuantity;
                                await e.Message.RespondAsync($"Sucessfully bought {nextSaleQuantity} {nextSaleItem}s for {(int)Math.Round(nextSaleQuantity * planetItem.pricePerUnit)} shmeckles.\n You now have {shmeckles} shmeckles!");
                            }
                        }
                    }
                }
            }
            return;
        }
        public async Task Sell(MessageCreateEventArgs e)
        {
            if (travelTimeLeft > 0)
            {
                await e.Message.RespondAsync($"Your in space fool, you cant sell anything");
                return;
            }
            foreach (planetItem planetItem in currLocation.items)
            {
                if (planetItem.itemName.ToLower() == nextSaleItem.ToLower()) //This is the item we want to manipulate
                {
                    foreach (playerItem playerItem in playerItems)
                    {
                        if (playerItem.itemName.ToLower() == nextSaleItem.ToLower()) //This is the item we're trying to manipulate
                        {
                            if (playerItem.quantityLeft < nextSaleQuantity)
                            {
                                await e.Message.RespondAsync($"You cant sell what you dont have :|");
                            }
                            else
                            {
                                playerItem.quantityLeft -= nextSaleQuantity;
                                shmeckles += (int)Math.Round(nextSaleQuantity * planetItem.pricePerUnit);

                                storage += nextSaleQuantity;

                                planetItem.quantityLeft += nextSaleQuantity;
                                await e.Message.RespondAsync($"Sucessfully sold {nextSaleQuantity} {nextSaleItem}s for {(int)Math.Round(nextSaleQuantity * planetItem.pricePerUnit)} shmeckles.\n You now have {shmeckles} shmeckles!");
                            }
                        }
                    }
                }
            }
            return;
        }

        public string nextSaleItem { get; set; }
        public int nextSaleQuantity { get; set; }
        public bool buying { get; set; } //Only true when the player is considering a purchase
        public bool selling { get; set; } //Only true when the player is considering a sale

        // ====================================Travel methods
        public planet currLocation { get; set; }
        public planet destination { get; set; }
        public double travelTimeLeft { get; set; } //In seconds
        public int shipSpeed { get; set; } //Parsecs per hour
        public void travelTick(double secondsPerTick)
        {
            if (travelTimeLeft > secondsPerTick + 5)
            {
                if (destination.yLoc - yLoc == 0)
                {
                    xLoc += (double)(shipSpeed * (double)secondsPerTick / 3600);
                }
                else if (destination.xLoc - xLoc == 0)
                {
                    yLoc += (double)(shipSpeed * (double)secondsPerTick / 3600);
                }
                else
                {
                    if (xLoc > destination.xLoc)
                        xLoc -= ((shipSpeed * secondsPerTick / 3600) * Math.Cos(Math.Atan((destination.yLoc - yLoc) / (destination.xLoc - xLoc))));
                    else
                        xLoc += ((shipSpeed * secondsPerTick / 3600) * Math.Cos(Math.Atan((destination.yLoc - yLoc) / (destination.xLoc - xLoc))));
                    if ((xLoc > destination.xLoc && yLoc > destination.yLoc) || (xLoc > destination.xLoc && yLoc < destination.yLoc))
                        yLoc -= (shipSpeed * secondsPerTick / 3600) * Math.Sin(Math.Atan((destination.yLoc - yLoc) / (destination.xLoc - xLoc)));
                    else
                        yLoc += (shipSpeed * secondsPerTick / 3600) * Math.Sin(Math.Atan((destination.yLoc - yLoc) / (destination.xLoc - xLoc)));
                }
                double distanceToDestination = ((double)destination.getDistance(xLoc, yLoc));

                travelTimeLeft = ((double)distanceToDestination) / shipSpeed * 3600; // Divid 1 parsec by 1 parsec per hour and you're left with 1 hour. 1 parsec by 2 parsecs per hour leaves you 1/2 hour. Multiply by 3600 to get seconds
                travelTimeLeft = (int)travelTimeLeft;
            }
            else if (travelTimeLeft <= secondsPerTick + 5) // We have arrived!
            {
                currLocation = destination;
                xLoc = currLocation.xLoc;
                yLoc = currLocation.yLoc;
                if (travelTimeLeft != -10)
                    responseChannel.SendMessageAsync($"Welcome to {currLocation.planetName} {thePlayer.Mention} \n" + currLocation.getRandomFlavorText());
                travelTimeLeft = -10;
            }
        }
        public async Task travelTo(planet destination, MessageCreateEventArgs e)
        {
            if (!(travelTimeLeft > 0))
            {
                xLoc = currLocation.xLoc;
                yLoc = currLocation.yLoc;
                this.destination = destination;

                double distanceToDestination = ((double)destination.getDistance(xLoc, yLoc));

                travelTimeLeft = ((double)distanceToDestination) / shipSpeed * 3600; // Divid 1 parsec by 1 parsec per hour and you're left with 1 hour. 1 parsec by 2 parsecs per hour leaves you 1/2 hour. Multiply by 3600 to get seconds
            }
            else //Changing destination midroute
            {
                this.destination = destination;

                double distanceToDestination = ((double)destination.getDistance(xLoc, yLoc));

                travelTimeLeft = ((double)distanceToDestination) / shipSpeed * 3600;
            }
        }
        public double xLoc { get; set; }
        public double yLoc { get; set; }
        // ==============================Misc
        public List<itemUpgrade> upgrades;
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
    public class itemUpgrade //Constant vision on a single item for the player. These are bought
    {
        public itemUpgrade(planet visionPlanet, planetItem visionItem)
        {
            this.visionItem = visionItem;
            this.visionPlanet = visionPlanet;
            upgradeLevel = 0;
        }
        public planet visionPlanet;
        public planetItem visionItem;
        public int upgradeLevel;
    }
    public class planet
    {
        public planet(string planetName, int distanceMultiplier) //Planet name determines location and item setups
        {
            this.orbitalRadius = distanceMultiplier;
            if (planetName.ToLower() == "medusa")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / 1800;
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", "avg", -5));
                items.Add(new planetItem("Spice", "high", -10));
                items.Add(new planetItem("Water", "extreme", -15));


                //Production Stuff
                items.Add(new planetItem("Electronics", "avg" , -5));
                items.Add(new planetItem("Ore", "low" , 35));
                items.Add(new planetItem("Fuel", "low", 15));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "avg", -5));
                items.Add(new planetItem("Aoemebae", "avg", -5));
            }
            else if (planetName.ToLower() == "rockefeller reach")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / (1800 * 2);
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", "avg", -5));
                items.Add(new planetItem("Spice", "avg", -5));
                items.Add(new planetItem("Water", "avg", -5));


                //Production Stuff
                items.Add(new planetItem("Electronics", "extreme", -15));
                items.Add(new planetItem("Ore", "high", -10));
                items.Add(new planetItem("Fuel", "low", 35));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "avg", -5));
                items.Add(new planetItem("Aoemebae", "low", 15));
            }
            else if (planetName.ToLower() == "arrakis")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / (1800 * 3);
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                items.Add(new planetItem("Food", "avg", -5));
                items.Add(new planetItem("Spice", "low", 35));
                items.Add(new planetItem("Water", "avg", -5));


                //Production Stuff
                items.Add(new planetItem("Electronics", "avg", -5));
                items.Add(new planetItem("Ore", "low", 15));
                items.Add(new planetItem("Fuel", "extreme", -35));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "avg", -5));
                items.Add(new planetItem("Aoemebae", "high", -10));
            }
            else if (planetName.ToLower() == "demeter")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / (1800 * 6);
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", "low", 35));
                items.Add(new planetItem("Spice", "avg", -5));
                items.Add(new planetItem("Water", "avg", -5));


                //Production Stuff
                items.Add(new planetItem("Electronics", "low", 15));
                items.Add(new planetItem("Ore", "avg", -5));
                items.Add(new planetItem("Fuel", "high", -10));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "extreme", -15));
                items.Add(new planetItem("Aoemebae", "avg", -5));
            }
            else if (planetName.ToLower() == "persepolis")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / (1800 * 5);
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", "extreme", -15));
                items.Add(new planetItem("Spice", "avg", -5));
                items.Add(new planetItem("Water", "low", 15));


                //Production Stuff
                items.Add(new planetItem("Electronics", "low", 35));
                items.Add(new planetItem("Ore", "avg", -5));
                items.Add(new planetItem("Fuel", "avg", -5));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "high", -10));
                items.Add(new planetItem("Aoemebae", "avg", -5));
            }
            else if (planetName.ToLower() == "shin-akihabara")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / (1800 * 6);
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", "avg", -5));
                items.Add(new planetItem("Spice", "low", 15));
                items.Add(new planetItem("Water", "avg", -5));


                //Production Stuff
                items.Add(new planetItem("Electronics", "high", -10));
                items.Add(new planetItem("Ore", "avg", -5));
                items.Add(new planetItem("Fuel", "avg", -5));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "low", 35));
                items.Add(new planetItem("Aoemebae", "extreme", -15));
            }
            else if (planetName.ToLower() == "delphi")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / (1800 * 7);
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", "low", 15));
                items.Add(new planetItem("Spice", "extreme", -15));
                items.Add(new planetItem("Water", "high", -10));


                //Production Stuff
                items.Add(new planetItem("Electronics", "avg", -5));
                items.Add(new planetItem("Ore", "avg", -5));
                items.Add(new planetItem("Fuel", "avg", -5));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "avg", -5));
                items.Add(new planetItem("Aoemebae", "low", 35));
            }
            else if (planetName.ToLower() == "aquarion")
            {
                this.planetName = planetName;
                orbitalPeriod = 0;
                orbitalSkew = 0;
                orbitalSpeed = 3.14159265358 / (1800 * 8);
                xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
                items = new List<planetItem>();

                //Essential Stuff
                items.Add(new planetItem("Food", "high", -10));
                items.Add(new planetItem("Spice", "avg", -5));
                items.Add(new planetItem("Water", "low", 35));


                //Production Stuff
                items.Add(new planetItem("Electronics", "avg", -5));
                items.Add(new planetItem("Ore", "extreme", -15));
                items.Add(new planetItem("Fuel", "avg", -5));

                //Exotic Stuff
                items.Add(new planetItem("Consumer Goods", "low", 15));
                items.Add(new planetItem("Aoemebae", "avg", -5));
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
            orbitalPeriod += secondsPerTick;
            xLoc = orbitalRadius * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed);
            yLoc = orbitalRadius * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed);
        }
        public double getDistance(double currXLocation, double currYLocation) //Returns the distance in parsecs between a planet and any given location
        {
            double distanceBetweenLocations = 0;
            if (currXLocation > xLoc && currYLocation > yLoc)
                distanceBetweenLocations = Math.Sqrt(Math.Pow(currXLocation - xLoc, 2) + Math.Pow(currYLocation - yLoc, 2));
            else if (currXLocation <= xLoc && currYLocation > yLoc)
                distanceBetweenLocations = Math.Sqrt(Math.Pow(xLoc - currXLocation, 2) + Math.Pow(currYLocation - yLoc, 2));
            else if (currXLocation > xLoc && currYLocation <= yLoc)
                distanceBetweenLocations = Math.Sqrt(Math.Pow(currXLocation - xLoc, 2) + Math.Pow(yLoc - currYLocation, 2));
            else if (currXLocation <= xLoc && currYLocation <= yLoc)
                distanceBetweenLocations = Math.Sqrt(Math.Pow(xLoc - currXLocation, 2) + Math.Pow(yLoc - currYLocation, 2));

            if (distanceBetweenLocations < .01)
                distanceBetweenLocations = 0;

            Console.WriteLine($"The distance between {planetName} and ({currXLocation}, {currYLocation}) is {distanceBetweenLocations}");

            return distanceBetweenLocations;
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
        public string ToString(List<itemUpgrade> upgrades, planet currLocation)
        {
            string retString = "";
            bool displayFlag = false;
            foreach (planetItem item in items)
            {
                displayFlag = false;
                foreach(itemUpgrade itemUpgrade in upgrades)
                {
                    if(itemUpgrade.visionPlanet == this && item == itemUpgrade.visionItem) // This player has an upgraded vision for this planet
                    {
                        displayFlag = true;
                        if (currLocation == this)
                            retString += $"\n_{item.quantityLeft}_ units of **{item.itemName}** at a price of *{(item.pricePerUnit).ToString("N2")} shmeckles* | Production Level: {item.productionRate} (Adv. Upgrade)";
                        else
                        {
                            if (itemUpgrade.upgradeLevel == 0)
                                retString += $"\n_{item.quantityLeft}_ units of **{item.itemName}** at a price of * ??? shmeckles | Not enough vision. Buy more upgrades!*";
                            else if (itemUpgrade.upgradeLevel == 1)
                                retString += $"\n_{item.quantityLeft}_ units of **{item.itemName}** at a price of *{(item.pricePerUnit).ToString("N2")} shmeckles*";
                            else
                                retString += $"\n_{item.quantityLeft}_ units of **{item.itemName}** at a price of *{(item.pricePerUnit).ToString("N2")} shmeckles* | Production Level: {item.productionRate} (Adv. Upgrade)";
                        }
                    }
                }
                if(displayFlag == false) //We didnt display the item via an upgrade
                {
                    if(currLocation != this) //We know absolutely nothing about this item.
                        retString += $"\n_??? units of **{item.itemName}** at a price of *??? shmeckles* | No vision. Buy upgrades!";
                    else
                    {
                        retString += $"\n_{item.quantityLeft}_ units of **{item.itemName}** at a price of *{(item.pricePerUnit).ToString("N2")} shmeckles*"; //We can display atleast basic info about this item because we're on the planet
                    }
                }
            }
            return retString;
        }
        public string getRandomFlavorText()
        {
            Random random = new Random();
            int randomNum = random.Next(1, 2);

            if(planetName == "Demeter")
            {
                switch(randomNum)
                {
                    case 1:
                        return "A tiny, verdant moon distantly orbiting the spice-world Arrakis, Demeter once produced nearly all the edible produce of the Orion system’s residents.  The coalition’s scorched-earth tactics have rendered the food produced here scarce and highly valuable.";
                    case 2:
                        return "The plains of Demeter are a welcome sight in the mostly-inhospitable Orion system.  A lush and habitable world orbiting Arrakis, Demeter produces the foodstuffs to feed most of Orion’s human residents.  Due to its small size, Demeter is populated mainly by wealthy landowners; this also leads to it having a disproportionately large amount of Protectorate loyalists present.";
                }
            }
            if (planetName == "Arrakis")
            {
                switch (randomNum)
                {
                    case 1:
                        return "A massive, rocky planet in Orion’s habitable zone, Arrakis plays host to a massive network of subterranean caves and crevasses.  Within these caves can be found the elusive “spice,” a powerful pharmaceutical substance said to grant users heightened physical and mental capabilities.  Rumors contend that a species of massive worms lie in wait under the desert sands.";
                    case 2:
                        return "Beneath the deserts of Arrakis, innumerable gigantic worms have carved out a network of subterranean tunnels, within which precious “spice” can be mined and extracted.  A fiercely traded commodity due to its performance-enhancing abilities, the spice from Arrakis is a valuable investment for any prospective trader.";
                }
            }
            if (planetName == "Aquarion")
            {
                switch (randomNum)
                {
                    case 1:
                        return "Comprised mostly of rock and ice, Aquarion plays an important role as a source of relatively pure water for the Orion system.  While inhospitable, its position relative to the outer reaches of Orion makes it enticing for common traders.";
                    case 2:
                        return "A dwarf planet in the furthest reaches of Orion’s orbit, Aquarion is composed of such a large amount of ice that it has become an important source of clean water for the system.  Gigantic crawling “spiders” are situated across Aquarion’s surface, melting and purifying the ice into potable water for transport.";
                }
            }
            if (planetName == "Persepolis")
            {
                switch (randomNum)
                {
                    case 1:
                        return "The massive and pleasantly habitable planet of Persepolis contains most of the Orion system’s civilian residents, as well as the bulk of its heavy industry.  Sensitive electronics and manufacturing components can be bought and sold here.  These are an invaluable commodity in distant locales where it would otherwise be difficult to manufacture them on-site.";
                    case 2:
                        return "The stark beacon of human civilization within the Orion system, Persepolis is home to the vast majority of the system’s population.  The density of its residents has led to the establishment of a hive of manufacturing and engineering, which produces much of the electronics and sensitive components used in construction projects across Orion.  Somewhat unpleasant conditions here have led to public discontent against a lack of Protectorate oversight, and have created a large number of PRC sympathizers, even after their defeat at Cannae.";
                }
            }
            if (planetName == "Medusa")
            {
                switch (randomNum)
                {
                    case 1:
                        return "Construction on an interstellar scale demands an abundance of raw material, and the rocky plains of Medusa provide a fitting source for this material.  While heavy and difficult to transport, the ores mined here are in demand across the Orion system.";
                    case 2:
                        return "A hunk of metallic ores and sturdy material close to the star Orion, Medusa produces much of the raw components needed to build structures across the system.  While heavy, these materials are always in demand and are particularly valuable on worlds that have the capability to refine them.";
                }
            }
            if (planetName == "Rockefellers Reach")
            {
                switch (randomNum)
                {
                    case 1:
                        return "Fuel and its refining remains an important component in the human industrial machine.  An old planet with a strange, nearly-gaseous core, Rockefeller’s reach is the site of a great deal of fuel mining and processing.";
                    case 2:
                        return "Without the massive refineries of Rockefeller’s Reach, the engine of human progress cannot run in Orion.  The only site capable of producing large enough quantities of fuel to support industrial operations in the system, the Reach remains a hotly contested prize among interstellar traders.";
                }
            }
            if (planetName == "Shin-Akihabara")
            {
                switch (randomNum)
                {
                    case 1:
                        return "While consumer goods are produced in typical quantities on Persepolis, its moon Shin-Akihabara produces a great deal of unconventional goods.  Protectorate public-morals codes prevent the export of some of the more seedy merchandise, but harmless products such as figurines and animated media have become something of a curiosity among residents of the Orion system.";
                    case 2:
                        return "An enclave of Orion’s more eccentric populace, Shin-Akihabara produces a wide variety of strange and exotic consumer goods.  These are less practical than those made on Persepolis, but the level of luxury they provide is a welcome comfort to the rich and poor alike.";
                }
            }
            if (planetName == "Delphi")
            {
                switch (randomNum)
                {
                    case 1:
                        return "A truly alien world on the outskirts of Orion, Delphi is the home of a vast hive-like population of amoebae which are theorized to possess near-human intelligence when arranged in large enough colonies.  They are the subject of fervent research among Orion scientists, and command a high price at the interstellar market.";
                    case 2:
                        return "Forays into space have revealed to humanity that interstellar life is not so rare as initially theorized- rather, it tends to be found in the form of single-celled organisms billions of years away from any meaningful evolution.  The amoebae found on Delphi are an outlier in this regard.  While their intelligence is difficult to comprehend by human standards, they are an important object of research across Protectorate-controlled space.";
                }
            }
            return $"Couldnt find flavortext for {planetName}. this is an error";
        }
        public string planetName;
        public List<planetItem> items;

        public double xLoc { get; set;} // Measured in parsecs. Obviously
        public double yLoc { get; set;}

        public int orbitalRadius;
        public int orbitalPeriod; //Time in seconds since the beginning of the game
        public int orbitalSkew; //Skew number to put the planet on a different part of its path at the start of the game
        public double orbitalSpeed; // Number of seconds to do a complete orbit

    };
    public class planetItem
    {
        public planetItem(string itemName, string demand, double productionRate)
        {
            //Different levels of demand.
            // 60 basePrice .120 expectedQuantity. | Low demand. Low Price - 2 per planet. stuff they produce
            //100 base 210 expect | Average Demand. Average Price - 4 per planet. Stuff moderatly consumed
            //160 base 350 expect | High Demand. High Price - 1 per planet stuff highly consumed
            //220 base 500 expect | Extreme Demand. - 1 per planet stuff extremely consumed
            //Using values outside of these will lead to undefined behavior

            if (demand.ToLower().Contains("low"))
            {
                this.basePrice = 60;
                this.quantityExpected = 120;
            }
            else if (demand.ToLower().Contains("avg"))
            {
                this.basePrice = 100;
                this.quantityExpected = 210;
            }
            else if (demand.ToLower().Contains("high"))
            {
                this.basePrice = 160;
                this.quantityExpected = 350;
            }
            else
            {
                this.basePrice = 220;
                this.quantityExpected = 500;
            }

            this.itemName = itemName;
            quantityLeft = quantityExpected;
            this.productionRate = productionRate;
            progressToNextItem = 0.0;

            pricePerUnit = ((-(Math.Log(quantityLeft / quantityExpected))) / (.02)) + quantityExpected / 2; //Really weird math function I know. But if the guide is stuck to above it gets values that make sense, shortages drive the price up faster than surpluses
        }
        public string itemName { get; private set; }
        public int quantityLeft { get; set; }
        public double productionRate; //How much of this produced per hour. Negative values mean it is consumed
        private double progressToNextItem; //Any number above 1 will be removed at the next production step and added to quantityLeft

        public double pricePerUnit { get; private set; }
        private int quantityExpected; //If quantityLeft < quantityExpected then the price goes up and vice versa

        private int basePrice;

        public void skewPrice(int secondsPerTick) //The higher the surplus or shortage the more the price will change
        {
            double targetPrice = 0;

            targetPrice = (-(Math.Log(quantityLeft/quantityExpected)))/(.02) + quantityExpected/2;
            Console.WriteLine($"Target Price is: {targetPrice}");
            if (targetPrice < (double)(basePrice - basePrice * .25))
                targetPrice = basePrice - basePrice * .25;
            if (targetPrice > (double)(basePrice + basePrice * .35))
                targetPrice = (basePrice + basePrice * .35);


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

        public string getRandomFlavorText()
        {
            Random random = new Random();
            int randomNum = random.Next(1, 3);

            if (itemName.ToLower() == "food")
                switch(randomNum)
                {
                    case 1:
                        return "*Fruit, vegetables, grains, and an assortment of meats and dairy.  Everything you need for a balanced diet.*";
                    case 2:
                        return "*In the outer and most inhospitable reaches of Orion, nonperishable food is a true necessity.  The miners on Aquarion will thank you for it.*";
                    case 3:
                        return "*Dehydrated, pickled, or salted food keeps well on long journeys throughout Orion.  While it isn’t the tastiest, nearly anyone will buy it.*";
                }
            if (itemName.ToLower() == "spice")
                switch (randomNum)
                {
                    case 1:
                        return "*The mythical physical and mental-enhancement substance mined from the depths of Arrakis.  Be careful not to overdo it.*";
                    case 2:
                        return "*Wars have been fought over this performance-enhancing stuff, and some argue it put Orion on the map for the Protectorate.  Be careful who you sell it to.*";
                    case 3:
                        return "*Otherwise known as “melange,” rumors about spice suggest that it may be able to unlock a level of latent psionic potential in human subjects.  Don’t think too hard about it.*";
                }
            if (itemName.ToLower() == "water")
                switch (randomNum)
                {
                    case 1:
                        return "*Refreshing, potable water.  A commodity across all of Orion.*";
                    case 2:
                        return "*It might not be cold, and the salt content might be somewhat unpleasant, but water is water.  And it sells well.*";
                    case 3:
                        return "*H\u2082O.  Water’s incompressible nature makes it difficult to transport in large quantities, but it’s valuable nonetheless.*";
                }
            if (itemName.ToLower() == "electronics")
                switch (randomNum)
                {
                    case 1:
                        return "*From sensitive microtechnology to heavy machinery, the products of Persepolis are a critical component of modern life.*";
                    case 2:
                        return "*Microchips, motors, engines small and large- vital pieces of commerce for Orion’s industrial operations.*";
                    case 3:
                        return "*Essential in the construction of delicate machinery are the electronics produced on Persepolis.  Handle with care.*";
                }
            if (itemName.ToLower() == "ore")
                switch (randomNum)
                {
                    case 1:
                        return "*Raw ore, sheet metal, girders and beams- everything you need to build a new bastion of civilization.  It’s pretty damned heavy, though.*";
                    case 2:
                        return "*When something’s built in Orion, chances are the stuff it’s made of came from Medusa.  Ore and sheet metal is heavy, but it’s always in demand.*";
                    case 3:
                        return "*From unprocessed ore to highly refined steel, the products of Medusa can be found from the arcologies on Demeter to the spiders of Aquarion.*";
                }
            if (itemName.ToLower() == "fuel")
                switch (randomNum)
                {
                    case 1:
                        return "*Hardly any vehicles will run without this stuff- especially not Renault-Williams FTL drives.  Always in demand.*";
                    case 2:
                        return "*This stuff is so volatile that it might make your ship implode if you breathe on it.  That being said, no engines will run without it.*";
                    case 3:
                        return "*Be careful you don’t let any stray sparks fall on a tank of this fuel, or it’ll tear your ship in half faster than a Protectorate artillery beam.*";
                }
            if (itemName.ToLower().Contains("good"))
                switch (randomNum)
                {
                    case 1:
                        return "*While some may argue that they’re not quite as useful to society as other essential goods and services, these always seem to sell quite well…*";
                    case 2:
                        return "*Figures and holotapes, games and diversions for the rich.  All neatly packaged and exorbitantly priced for resale.*";
                    case 3:
                        return "*Some may ask: is civilization really better off with these kitschy trinkets?  You’re not as concerned as they are- this stuff sells like hotcakes.*";
                }
            if (itemName.ToLower() == "amoebae")
                switch (randomNum)
                {
                    case 1:
                        return "*Frozen, well-sealed vats of amoebae from Delphi.  Make sure not to spill them.*";
                    case 2:
                        return "*Sometimes you get the sneaking suspicion that these amoebae are looking at you when you pass by them.  Maybe you could throw a blanket over the canisters.*";
                    case 3:
                        return "*You sometimes wonder if it’s ethically wrong to transport these amoebae, since modern science suggests they’re probably collectively smarter than you.  A question for another time.*";
                }

            return "This text is the result of error. If you see this, I didnt code something right";
        }
    }
    public class moon : planet
    {
        public moon(string planetName, planet orbitingBody) : base(planetName, 1)
        {
            this.orbitingBody = orbitingBody;
        }
        planet orbitingBody;
        new public void tickPlanet(int secondsPerTick)
        {
            foreach (planetItem item in items)
            {
                item.produce(secondsPerTick);
                item.skewPrice(secondsPerTick);
            }
            orbitalPeriod += secondsPerTick;
            xLoc = orbitingBody.xLoc + (.15 * Math.Cos((orbitalPeriod + orbitalSkew) * orbitalSpeed));
            yLoc = orbitingBody.yLoc + (.15 * Math.Sin((orbitalPeriod + orbitalSkew) * orbitalSpeed));
        }
    }
}
