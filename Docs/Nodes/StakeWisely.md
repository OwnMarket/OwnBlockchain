# Stake Wisely

This article explains the importance of carefully choosing validators to delegate stake to, as well as monitoring if they are actively proposing blocks, to avoid having fake validators and thereby jeopardizing the whole network.


## How it works?

Validators are blockchain nodes responsible for creating new blocks and moving the network forward. To incentivize validators to process transactions and create new blocks, they are rewarded with the transaction fees.
Since not all validators are able to provide enough CHX at stake to become active validators and participate in consensus, staking mechanism is implemented to enable everyone to delegate their CHX to the validators they choose, thereby enabling them to become active and propose blocks.
To incentivize stakers to delegate their CHX, validators can choose to distribute a percentage of collected rewards to them.

Once validator has enough CHX at stake and becomes active, it has the right to participate in consensus and propose blocks. However, since **active** validators form the quorum during the voting process, a validator also has the **obligation** to participate in consensus and cast its votes.

Being a distributed system, blockchain network cannot rely on all active validators being available at any point in time, and must be able to tolerate faults up to a certain extent. The BFT consensus protocol, as implemented in WeOwn public blockchain, tolerates up to 1/3 faulty validators.


## Why it is important?

The fact that a validator is registered (appearing on the list of validators in the [wallet](https://wallet.weown.com/info/validator) and [explorer](https://explorer.weown.com/validators)), delegated to and activated, does not guarantee the existence of the actual machine with the operational node software behind it.

If there are 100 active validators registered in the blockchain, network will continue to function as long as at least 67 out of 100 validators are working properly and proposing and voting on blocks.
If more than 33 out of 100 validators are not working properly or are fake (registered and active without actual node running), the network will halt and no further blocks will be created.

Sometimes economic incentives for staking are not significant because the amount of collected reward is low. However, if the blockchain halts, the amount of collected reward will be exactly zero, because no new blocks will be created. In short, if blockchain halts, everyone looses!

**If you delegate your CHX to a validator, thereby enabling it to become active, you should also monitor if that validator is actually running and actively proposing blocks.
Delegating the stake to a fake active validator is damaging the network and such stakes should be revoked as soon as possible, to deactivate such fake validators.**


## How to choose a validator?

The most relevant criteria, when choosing the validators to delegate stake to is:
- number of proposed blocks in last N days
    (don't delegate stake to an active validator if it didn't propose any blocks recently)
- amount of distributed rewards in last N days
    (the more rewards distributed, the more you will receive)
- shared reward percentage
    (keep in mind that a bigger percentage doesn't necessarily mean more reward, especially if the total stake is high)
- total stake
    (the bigger the total stake, the smaller your part in it)

**In short, look for a validator with total stake close to 500k and high shared reward percentage. If delegating to an *active* validator, make sure it is actually proposing blocks and distributing rewards.**

[Blockchain Explorer](https://explorer.weown.com/validators) and [Staking App](https://play.google.com/store/apps/details?id=com.weown.stakingtool&hl=en_US) provide validator statistics and monitoring capabilities, which can help making an informed decision.

It is of utmost importance to revoke the stake from a validator if it is **active but not proposing any blocks**.

Since it is not possible to know for sure if an **inactive** validator is fake or not, it is recommended to check its address' history in the explorer, before choosing to delegate stake to it.
Also, many validator operators are active in the [Telegram channel](https://web.telegram.org/#/im?p=@OwnStakingCHX), where you can request more information about a certain validator if in doubt.

*Stake wisely!*
