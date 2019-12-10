# Validator Deregistration

This document describes the procedure for deregistration of the validator node and removing its info from the blockchain state.


## Steps

1. Initiate validator deactivation
2. Wait for validator deactivation
3. Uninstall node software
4. Wait for deposit to unlock
5. Remove validator registration info from blockchain

**IMPORTANT: As long as the validator is marked as active, it should also be kept operational (node should not be stopped).**


### Step 1: Initiate validator deactivation

If the validator is active, it first has to be deactivated.
This can be achieved in two ways:

- Submitting a transaction with `ConfigureValidator` action having `IsEnabled` parameter set to `false`.
  (This is the recommended method.)
- Reducing delegated stakes to under required threshold of 500k CHX.
  (This method is less reliable, because someone can delegate stake to the validator again before it gets deactivated.)


### Step 2: Wait for validator deactivation

After the deactivation is initiated, it is necessary to wait until the next configuration block for the deactivation to take effect and for validator to be marked as inactive.


### Step 3: Uninstall node software

Once the validator is inactive, the corresponding node software can be uninstalled by following the procedure described in [node removal document](NodeRemoval.md).


### Step 4: Wait for deposit to unlock

To remove the validator info from the blockchain, and thereby release the deposit, it is necessary to wait until the deposit is unlocked.
Inactive validator's deposit is locked for certain number of configuration blocks (10 configuration blocks on MainNet).


### Step 5: Remove validator registration info from blockchain

Once the deposit is unlocked, validator info can be removed from the blockchain state, thereby releasing the deposit, by submitting a transaction containing `RemoveValidator` action. This action doesn't have any parameters.

Upon execution of this transaction, all delegated stakes will be returned from the removed validator to the corresponding stakers.
