# Frequently Asked Questions About Nodes and Validators

This document covers some frequent questions about operating nodes and validators.


## How to change the validator network address?

1. Create new DNS entry and point it to the same IP address the current one is pointing to.
2. Make sure your node is accessible on that new DNS name. (e.g. by connecting to its API).
3. Set `PublicAddress` in config file to the new DNS name and restart the node.
4. Submit a TX with `ConfigureValidator` action specifying new network address (port can stay the same).
5. Wait for next config block to verify the change and make sure validator is still proposing blocks and earning rewards.
6. Remove old DNS name (this is not blockchain related and it is up to you though).
