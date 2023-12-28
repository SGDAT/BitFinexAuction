/// This code was written by Samson Gabriels
/// https://github.com/SGDAT/BitFinexAuction
/// 

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

        //the list of usernames and auctions are best stored in some kind of database but for time sake, I am using lists.
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
                message.Auctions.AddRange(Auctions);

                // Send to connected clients
                foreach (var stream in announceResponseStreams)
                {
                    await stream.WriteAsync(message);
                }
            }

        }

        /// <summary>
        /// store the connections to any new users to connect
        /// </summary>
        /// <param name="requestStream">the incomming request stream</param>
        /// <returns></returns>
        private AnnounceResponse RegisterUser(IAsyncStreamReader<LobbyMessage> requestStream)
        {
            // Get the new user message/username from the request stream
            var messageFromClient = requestStream.Current;

            if (!UserNames.Any(u => u.Username == messageFromClient.Username))
            {
                //Generate a new id
                nextID++;
                LobbyUser lobbyUser = new LobbyUser()
                {
                    Id = nextID, //give them a new ID
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

        /// <summary>
        /// Respond with all the auctions available on the server
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override Task<GetAuctionsResponse> GetAuctions(LobbyMessage request, ServerCallContext context)
        {
            GetAuctionsResponse response = new GetAuctionsResponse();
            response.Auctions.AddRange(Auctions);

            return Task.FromResult(response);
        }

        /// <summary>
        /// Handle a new bid sent from a client
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<GetAuctionsResponse> SubmitBid(BidMessage request, ServerCallContext context)
        {
            //get the auction that the bid is for
            AuctionItem auction = Auctions.FirstOrDefault(a => a.Id == request.AuctionItemId);

            //if the auction exists, continue
            if(auction != null)
            {
                //get the user with the id
                LobbyUser user = UserNames.FirstOrDefault(u => u.Id == request.LobbyUserId);

                //create a new bid and reference the user and their bid amount
                Bid newBid = new Bid
                {
                    LobbyUser = user,
                    BidAmount = request.BidAmount,
                };
                auction.Bids.Add(newBid);

                //if the new bid is bigger that current highest bid, set user and bid amount
                if (request.BidAmount > auction.Cost)
                {
                    auction.Winner = user.Username;
                    auction.Cost = request.BidAmount;
                }

                //tell the connected clients about the latest auction activity
                await _UpdateClientsAuctions();
            }

            GetAuctionsResponse response = new GetAuctionsResponse();
            response.Auctions.AddRange(Auctions);

            return await Task.FromResult(response);
        }

        public override async Task<GetAuctionsResponse> CreateAuction(CreateAuctionMessage request, ServerCallContext context)
        {
            //get the user with the id
            LobbyUser user = UserNames.FirstOrDefault(u => u.Id == request.LobbyUserId);

            //add some extra details and add to our auctions
            request.AuctionItem.Owner = user.Username;
            request.AuctionItem.OwnerId = user.Id;
            request.AuctionItem.Id = nextAuctionID++;
            Auctions.Add(request.AuctionItem);

            GetAuctionsResponse response = new GetAuctionsResponse();
            response.Auctions.AddRange(Auctions);

            //tell the connected clients about the latest auction activity
            await _UpdateClientsAuctions();

            return await Task.FromResult(response);
        }

        /// <summary>
        /// Sent connected users about the latest auction activity
        /// </summary>
        /// <returns></returns>
        private async Task _UpdateClientsAuctions()
        {
            AnnounceResponse message = new AnnounceResponse()
            {
                Id = -1 //we dont want to set any new userIDs with the next call
            };
            message.Users.AddRange(UserNames);
            message.Auctions.AddRange(Auctions);

            // Send to connected clients
            foreach (var stream in announceResponseStreams)
            {
                await stream.WriteAsync(message);
            }
        }

        /// <summary>
        /// Respond with the result of closing the auction
        /// </summary>
        /// <param name="request"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public override async Task<GetAuctionsResponse> CloseAuction(CloseAuctionMessage request, ServerCallContext context)
        {
            //get the auction that the bid is for
            AuctionItem auction = Auctions.FirstOrDefault(a => a.Id == request.AuctionItem.Id);

            //if the auction exists, continue to close it
            if (auction != null)
            {
                //final check to ensure only the owner of the auction can close it
                if(auction.OwnerId == request.LobbyUserId)
                    auction.IsOpen = false;
            }

            //tell the connected clients about the latest auction activity
            await _UpdateClientsAuctions();

            //finally, remove the auction from our list if its been closed
            if (auction != null)
            {
                if (!auction.IsOpen)
                    Auctions.Remove(auction);
            }
            return await base.CloseAuction(request, context);
        }

    }
}
