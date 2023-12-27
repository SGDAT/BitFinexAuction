// See https://aka.ms/new-console-template for more information
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

    static int position;
    static Enums.Mode mode;
    static int userID = -1;
    static string userName = null;

    static List<LobbyUser> UserNames = new List<LobbyUser>();
    static List<AuctionItem> Auctions = new List<AuctionItem>();

    private static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, Bitfinex! Welcome to 'BitfeBay'");

        await GetUsername();
        await InitializeGrpc();



        //var message = new LobbyMessage
        //{
        //    Username = "username",
        //};

        //if (_call != null)
        //{
        //    await _call.RequestStream.WriteAsync(message);
        //}
    }

    async static Task InitializeGrpc()
    {

        // Create a channel
        channel = new Channel(Host + ":" + Port, ChannelCredentials.Insecure);

        try
        {
            mode = Enums.Mode.Client;
            await AnnouncePresence();
            Task.Run(() => InitialiseAsClient());
            ShowMenu();
        }
        catch (RpcException e)
        {
            if(e.StatusCode == StatusCode.Unavailable)
            {
                position = 0;
                mode = Enums.Mode.ServerClient;
                await InitialiseAsServer();
                await Task.Delay(1000);
                Task.Run(()=> InitialiseAsClient());

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

        // Open a connection to the server
        _announceCall = _lobbyService.Announce();


        while (await _announceCall.ResponseStream.MoveNext(CancellationToken.None))
        {
            var serverMessage = _announceCall.ResponseStream.Current;
            userID = serverMessage.Id;
            var otherClientMessage = serverMessage.Message;
            var displayMessage = string.Format($"{otherClientMessage.Username} is here.");
            Console.WriteLine(displayMessage);

            UserNames = new List<LobbyUser>(serverMessage.Users);
            ShowAllUsers();

        }

    }


    private static void ShowAllUsers()
    {
        var otherUsers = $"All connected users: {string.Join(",", UserNames.Select(u => u.Username))}";
        Console.WriteLine(otherUsers);
    }

    private static void ShowAllAuctions()
    {
        var otherAuctions = $"All available auctions: {string.Join(",", Auctions.Select(a=> $"id: {a.Id} - name: {a.ProductName}"))}";
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

            // Use a switch statement to execute different actions based on the user's choice
            switch (choice)
            {
                case "1":
                    // Open Auction
                    Console.WriteLine("Please enter the product name");
                    string productName = Console.ReadLine();
                    Console.WriteLine("Please enter the starting cost");
                    int cost = Int32.Parse(Console.ReadLine());

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

                    Auctions.Clear();
                    Auctions.AddRange(createAuctionsResponse.Auctions);


                    break;
                case "2":
                    //Update Auction
                    GetAuctionsResponse getAuctionsResponse = _lobbyService.GetAuctions(new LobbyMessage());

                    Auctions.Clear();
                    Auctions.AddRange(getAuctionsResponse.Auctions);
                    break;
                case "3":
                    // Bid on Auction
                    break;
                case "4":
                    // Close Auction
                    break;
                default:
                    // Handle invalid input
                    Console.WriteLine("Invalid option. Please try again.");
                    break;
            }
        }

        
    }



}