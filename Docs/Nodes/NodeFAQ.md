# Frequently Asked Questions About Nodes and Validators

This document covers some frequent questions about operating nodes and validators.


## How to move a validator node to a new machine?

- Install a blockchain node on the new machine.
- Don't change the configuration at this point.
- Start the node and let it fully synchronize.
- Stop the old node.
- Remove `PublicAddress` and `ValidatorPrivateKey` settings from the configuration file of the old node.
- Change the DNS entry to point to the IP of the new machine.
- Wait for **at least one block** to pass. (more details about this in another answer below).
- Enter `PublicAddress` and `ValidatorPrivateKey` settings in the configuration file of the new node.
- Restart the new node.


## Why do I have to wait for at least one block to pass before starting a new node using the PK from the old node?

A validator is participating in consensus protocol by sending consensus messages signed using its private key,
which is configured in the `ValidatorPrivateKey` setting in the configuration file.
To prevent network from forking, which would result in the network halt, it is important to prevent conflicting messages,
also known as *equivocation*.

Equivocation can occur either intentionally due to the malicious code execution,
or unintentionally due to the same private key being used in two different instances of the validator node.
Equivocation can induce damage, regardless of being initiated intentionally or unintentionally.

To protect the network from bad effects of the equivocation, nodes are programmed to detect the conflicting messages
and disable the sending validator by blacklisting it (thereby ignoring its subsequent messages),
as well as penalizing such a behavior by slashing the validator's security deposit.

Having all the above explained, let's apply it to the answer to the actual question:

- Let's say the validator was in the process of voting for block 10, and it sent its vote for the corresponding block hash.
- Operator stopps the old node and immediately starts the new one.
- Since network is stil voting on block 10, new validator might send a different vote about block 10 than the old one,
  which will result in the two conflicting messages for the same block from the same validator.
- Other validators will detect two conflicting messages from the same validator and will slash its deposit.

If the operator, after stopping the old node, had waited for one block before starting the new one,
new node would already be on block 11 and would not have a chance to send a vote for block 10.


## How to change the validator network address?

- Create new DNS entry and point it to the same IP address the current one is pointing to.
- Make sure your node is accessible on that new DNS name. (e.g. by connecting to its API).
- Set `PublicAddress` in config file to the new DNS name and restart the node.
- Submit a TX with `ConfigureValidator` action specifying new network address (port can stay the same).
- Wait for next config block to verify the change and make sure validator is still proposing blocks and earning rewards.
- Remove old DNS name (this is not blockchain related and it is up to you though).
