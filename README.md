Hi bitfinex.

This task took me a few hours extra to complete. However, you will find that the peer-to-peer like function as well as the auction functionality. The first instance behaves as a server if it cannot connect to any servers and every other instance created connects to the first instance.

This is now complete. My last commit update simply contains comments in the code for better understanding.

Update 1: I had some thoughts about the P2P requirements. I am assuming this is referring to something like bittorrent where there is a tracker and peers connect to it for connections to connect to. However, on thinking further, this may simply require some kind of connectivity from one peer to another. But as mentioned earlier, although any peer can become a "server", each instance is not actively a server in itself accepting connections from other instances. With this in mind now, I would have ensured each instance spun up a server in the background similar to the way I do now but each server would have a unique port number by which each instance can be accessed by. There may be a better way to access each peer but as I have had to learn gRPC and implement it within the 8 hour period, I was not able to implement "the best" solution.

The task itself also gave me a pretty strong idea as to how your systems work which makes sense and is quite genius to be able to use the same proto files across different environments and programming languages.

I hope my above thoughts are acceptable and would love to discuss this with you further.

Kind regards,

Samson
