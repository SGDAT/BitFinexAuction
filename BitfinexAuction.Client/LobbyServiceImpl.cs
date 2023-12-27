using Google.Protobuf.Collections;
using Grpc.Core;
using Lobby;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitfinexAuction.Client
{
    public class LobbyServiceImpl : LobbyService.LobbyServiceBase
    {
        int nextID = 0;
        int nextAuctionID = 0;
        List<LobbyUser> UserNames = new List<LobbyUser>();
        List<AuctionItem> Auctions = new List<AuctionItem>();

        private static List<IServerStreamWriter<AnnounceResponse>> announceResponseStreams = new List<IServerStreamWriter<AnnounceResponse>>();

        public override async Task Announce(IAsyncStreamReader<LobbyMessage> requestStream, IServerStreamWriter<AnnounceResponse> responseStream, ServerCallContext context)
        {
            // Keep track of connected clients
            announceResponseStreams.Add(responseStream);

            while (await requestStream.MoveNext(CancellationToken.None))
            {
                AnnounceResponse message = RegisterUser(requestStream);

                // Send to connected clients
                foreach (var stream in announceResponseStreams)
                {
                    await stream.WriteAsync(message);
                }
            }

        }

        private AnnounceResponse RegisterUser(IAsyncStreamReader<LobbyMessage> requestStream)
        {
            // Get the client message from the request stream
            var messageFromClient = requestStream.Current;

            if (!UserNames.Any(u => u.Username == messageFromClient.Username))
            {
                nextID++;
                LobbyUser lobbyUser = new LobbyUser()
                {
                    Id = nextID,
                    Username = messageFromClient.Username,
                };

                UserNames.Add(lobbyUser);
            }

            // Create a server message that wraps the client message
            var message = new AnnounceResponse
            {
                Id = nextID,
                Message = messageFromClient,
            };
            message.Users.AddRange(UserNames);
            return message;
        }

        public override Task<GetAuctionsResponse> GetAuctions(LobbyMessage request, ServerCallContext context)
        {
            GetAuctionsResponse response = new GetAuctionsResponse();
            response.Auctions.AddRange(Auctions);

            return Task.FromResult(response);
        }

        public override Task<GetAuctionsResponse> SubmitBid(BidMessage request, ServerCallContext context)
        {
            AuctionItem auction = Auctions.FirstOrDefault(a => a.Id == request.AuctionItemId);
            LobbyUser user = UserNames.FirstOrDefault(u => u.Id == request.LobbyUserId);
            Bid newBid = new Bid
            {
                LobbyUser = user,
                BidAmount = request.BidAmount,
            };
            auction.Bids.Add(newBid);
            if(request.BidAmount > auction.Cost)
            {
                auction.Winner = user.Username;
                auction.Cost = request.BidAmount;
            }

            GetAuctionsResponse response = new GetAuctionsResponse();
            response.Auctions.AddRange(Auctions);

            return Task.FromResult(response);
        }

        public override Task<GetAuctionsResponse> CreateAuction(CreateAuctionMessage request, ServerCallContext context)
        {
            LobbyUser user = UserNames.FirstOrDefault(u => u.Id == request.LobbyUserId);
            request.AuctionItem.Owner = user.Username;
            request.AuctionItem.Id = nextAuctionID++;
            Auctions.Add(request.AuctionItem);

            GetAuctionsResponse response = new GetAuctionsResponse();
            response.Auctions.AddRange(Auctions);

            return Task.FromResult(response);
        }



        public override Task<GetAuctionsResponse> CloseAuction(CloseAuctionMessage request, ServerCallContext context)
        {
            AuctionItem auction = Auctions.FirstOrDefault(a => a.Id == request.LobbyUserId);
            auction.IsOpen = false;

            return base.CloseAuction(request, context);
        }
    }
}
