/// This code was written by Samson Gabriels
/// https://github.com/SGDAT/BitFinexAuction
/// 

using BitfinexAuction.Client;
using Grpc.Core;
using Lobby;
using System;
using System.Xml.Linq;


internal class Program
{
    const string Host = "localhost";
    const int Port = 1337;

    static LobbyService.LobbyServiceClient _lobbyService;
    static AsyncDuplexStreamingCall<LobbyMessage, AnnounceResponse> _announceCall;
    static Channel channel;

    static int userID = -1;
    static string userName = null;

    static List<LobbyUser> UserNames = new List<LobbyUser>();
    static List<AuctionItem> Auctions = new List<AuctionItem>();

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, Bitfinex! Welcome to 'BitfeBay'");

        await GetUsername();
        await InitializeGrpc();


    }

    async static Task InitializeGrpc()
    {

        // Create a channel
        channel = new Channel(Host + ":" + Port, ChannelCredentials.Insecure);

        try
        {
            //connect to the server
            await AnnouncePresence();
            Task.Run(() => InitialiseAsClient());
            ShowMenu();
        }
        catch (RpcException e)
        {
            //if the server does not exist, create a new server
            if(e.StatusCode == StatusCode.Unavailable)
            {
                await InitialiseAsServer();
                await Task.Delay(1000);

                //now that we have a new server (on this client), start listening as if we are a client
                Task.Run(()=> InitialiseAsClient());

                //connect to myself as a client
                await AnnouncePresence();

                ShowMenu();
            }

        }
        catch (Exception e)
        {
            
        }

    }

    private static async Task AnnouncePresence()
    {
        // Create a username message
        var message = new LobbyMessage
        {
            Username = userName
        };
        // Create a client with the channel
        if (_lobbyService == null)
            _lobbyService = new LobbyService.LobbyServiceClient(channel);

        // Open a connection to the server
        _announceCall = _lobbyService.Announce();

        // Send the message
        if (_announceCall != null)
        {
            await _announceCall.RequestStream.WriteAsync(message);
        }
    }

    static async Task InitialiseAsServer()
    {
        // Build server
        var server = new Server
        {
            Services = { LobbyService.BindService(new LobbyServiceImpl()) },
            Ports = { new ServerPort(Host, Port, ServerCredentials.Insecure) }
        };

        // Start server
        server.Start();


        Console.WriteLine("I am the server. BitfeBay listening on port " + Port);
    }

    async static Task InitialiseAsClient()
    {
        // Create a client with the channel
        if(_lobbyService == null)
            _lobbyService = new LobbyService.LobbyServiceClient(channel);

        //Process messages sent back from server
        while (await _announceCall.ResponseStream.MoveNext(CancellationToken.None))
        {
            var serverMessage = _announceCall.ResponseStream.Current;

            //set our own user id
            if(serverMessage.Id >= 0 && userID <= 0)
                userID = serverMessage.Id;

            //if there are new clients connected, let us know about it
            var otherClientMessage = serverMessage.Message;
            if (otherClientMessage != null)
            {
                var displayMessage = string.Format($"{otherClientMessage.Username} is here.");
                Console.WriteLine(displayMessage);
            }

            //store the total list of users sent back from server
            UserNames = new List<LobbyUser>(serverMessage.Users);

            //store any auctions the server has responded with
            Auctions = new List<AuctionItem>(serverMessage.Auctions);

            //if an auction is finished, let us know about it and the winner details
            AuctionItem finishedAuction = Auctions.FirstOrDefault(a => !a.IsOpen);
            if(finishedAuction != null)
            {
                var displayMessage = string.Format($"The auction for {finishedAuction.ProductName} has ended, sold to {finishedAuction.Winner} for {finishedAuction.Cost}");

                Console.WriteLine(displayMessage);

                Auctions.Remove(finishedAuction);
            }
            ShowAllUsers();
            ShowAllAuctions();

        }

    }

    /// <summary>
    /// Show us a list of user names
    /// </summary>
    private static void ShowAllUsers()
    {
        var otherUsers = $"All connected users: {string.Join(",", UserNames.Select(u => u.Username))}";
        Console.WriteLine(otherUsers);
    }

    /// <summary>
    /// show us a list of all auctions
    /// </summary>
    private static void ShowAllAuctions()
    {
        var otherAuctions = $"All available auctions: {string.Join(",", Auctions.Select(a=> $"id: {a.Id} - name: {a.ProductName} - current bid: {a.Cost} - winning: {a.Winner}"))}";
        Console.WriteLine(otherAuctions);
    }

    async static Task GetUsername()
    {
        // Ask the user to enter their name
        while (string.IsNullOrEmpty(userName))
        {
            Console.WriteLine("Enter your Username:");
            userName = Console.ReadLine();
        }

        Console.WriteLine("Hello, " + userName);
    }

    /// <summary>
    /// show the interface for users
    /// </summary>
    static void ShowMenu()
    {

        // Declare a variable to store the user's choice
        string choice = null;

        // Use a loop to repeat the menu until the user exits
        while (choice != "x")
        {
            ShowAllUsers();
            ShowAllAuctions();

            // Display the menu options
            Console.WriteLine("What would you like to do?");
            Console.WriteLine("1 - Open Auction");
            Console.WriteLine("2 - Update Auctions");
            Console.WriteLine("3 - Bid on Auction");
            Console.WriteLine("4 - Close Auction");


            // Get the user's input
            choice = Console.ReadLine();
            int auctionId;

            try
            {
                // Use a switch statement to execute different actions based on the user's choice
                switch (choice)
                {
                    case "1":
                        // Open Auction
                        Console.WriteLine("Please enter the product name");
                        string productName = Console.ReadLine();
                        Console.WriteLine("Please enter the starting cost");
                        float cost = float.Parse(Console.ReadLine());

                        GetAuctionsResponse createAuctionsResponse = _lobbyService.CreateAuction(new CreateAuctionMessage
                        {
                            AuctionItem = new AuctionItem
                            {
                                Cost = cost,
                                ProductName = productName,
                                IsOpen = true,
                            },
                            LobbyUserId = userID
                        });

                        //update our list of auctions
                        Auctions.Clear();
                        Auctions.AddRange(createAuctionsResponse.Auctions);


                        break;
                    case "2":
                        //get an updated list of auctions
                        GetAuctionsResponse getAuctionsResponse = _lobbyService.GetAuctions(new LobbyMessage());

                        //update our list of auctions
                        Auctions.Clear();
                        Auctions.AddRange(getAuctionsResponse.Auctions);
                        break;
                    case "3":
                        // Bid on Auction
                        Console.WriteLine("Please auction id");
                        auctionId = Int32.Parse(Console.ReadLine());

                        Console.WriteLine("Please enter your bid");
                        float bidAmount = float.Parse(Console.ReadLine());

                        GetAuctionsResponse submitBidResponse = _lobbyService.SubmitBid(new BidMessage
                        {
                            AuctionItemId = auctionId,
                            BidAmount = bidAmount,
                            LobbyUserId = userID
                        });

                        //update our list of auctions
                        Auctions.Clear();
                        Auctions.AddRange(submitBidResponse.Auctions);
                        break;
                    case "4":
                        // Close Auction
                        Console.WriteLine("Please auction id");
                        auctionId = Int32.Parse(Console.ReadLine());

                        AuctionItem closingAuction = Auctions.FirstOrDefault(a => a.Id == auctionId && a.OwnerId == userID);
                        if (closingAuction != null)
                        {
                            GetAuctionsResponse closeAuctionResponse = _lobbyService.CloseAuction(new CloseAuctionMessage
                            {
                                LobbyUserId = userID,
                                AuctionItem = closingAuction,
                            });

                            //update our list of auctions
                            Auctions.Clear();
                            Auctions.AddRange(closeAuctionResponse.Auctions);
                        }
                        else
                        {
                            Console.WriteLine("This is not your auction.");
                        }
                        break;
                    default:
                        // Handle invalid input
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("There seems to be a problem. Please try again.");

            }

        }

        
    }


}