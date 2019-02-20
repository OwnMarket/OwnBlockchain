const fs = require('fs')
const chainiumSdk = require('chainium-sdk/src/index')

////////////////////////////////////////////////////////////////////////////////////////////////////
// Test Parameters
////////////////////////////////////////////////////////////////////////////////////////////////////

const networkCode = "OWN_PUBLIC_BLOCKCHAIN_DEVNET"
const genesisPrivateKey = "ZXXkM41yHhkzb2k5KjeWuGCzYj7AXAfJdMXqKM4TGKq"
const genesisAddress = "CHGeQC23WjThKoDoSbKRuUKvq1EGkBaA5Gg"

const validatorAddresses =
    [
        "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSw42Q2",
        "CHXr1u8DvLmRrnBpVmPcEH43qBhjez6dc4N",
        "CHN5FmdEhjKHynhdbzXxsNB35oxL559gRLH",
        "CHStDQ5ZFeFW9rbMhw83f7FXg19okxVVScM"
    ]

const validatorStake = 500000
const validatorDeposit = 5000
const initialBalance = 10000
const walletCount = 100
const transfersPerWallet = 10
const actionsPerTx = 1
const actionFee = 0.001

const outputFile = "../run_test.sh"

////////////////////////////////////////////////////////////////////////////////////////////////////
// Functions
////////////////////////////////////////////////////////////////////////////////////////////////////

function composeInitialTx(validators, recipients) {
    const actions = []

    validators.forEach(v => {
        actions.push({
            ActionType: "DelegateStake",
            ActionData: {
                ValidatorAddress: v,
                Amount: validatorStake
            }
        })
        actions.push({
            ActionType: "TransferChx",
            ActionData: {
                RecipientAddress: v,
                Amount: validatorDeposit
            }
        })
    })

    recipients.forEach(r => {
        actions.push({
            ActionType: "TransferChx",
            ActionData: {
                RecipientAddress: r,
                Amount: initialBalance
            }
        })
    })

    return {
        SenderAddress: genesisAddress,
        Nonce: 1,
        ActionFee: actionFee,
        Actions: actions
    }
}

function composeTx(senderAddress, nonce, recipientAddress, amount) {
    var tx = {
        SenderAddress: senderAddress,
        Nonce: nonce,
        ActionFee: actionFee,
        Actions: []
    }

    Array(actionsPerTx).fill().forEach(_ => {
        tx.Actions.push(
            {
                ActionType: "TransferChx",
                ActionData: {
                    RecipientAddress: recipientAddress,
                    Amount: (amount / actionsPerTx)
                }
            }
        )
    })

    return tx
}

function signTx(networkCode, privateKey, tx){
    const txRaw = chainiumSdk.crypto.utf8ToHex(JSON.stringify(tx));
    const txBase64 = chainiumSdk.crypto.encode64(txRaw);
    const signature = chainiumSdk.crypto.signMessage(networkCode, privateKey, txRaw);

    return JSON.stringify({
        tx: txBase64,
        signature: signature
    })
}

let invocation = 0
function txToCommand(tx) {
    invocation++
    let port = 10701 + (invocation % 4)
    //return `curl -H "Content-Type: application/json" -d ${JSON.stringify(tx)} http://localhost:${port}/tx\n`
    return `curl -H "Content-Type: application/json" -d @- http://localhost:${port}/tx <<JSON\n${tx}\nJSON\n`
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Compose
////////////////////////////////////////////////////////////////////////////////////////////////////

const wallets = Array(walletCount).fill().map(_ => chainiumSdk.crypto.generateWallet())

const initialTx = signTx(
    networkCode,
    genesisPrivateKey,
    composeInitialTx(validatorAddresses, wallets.map(w => w.address)))

fs.writeFileSync(outputFile, txToCommand(initialTx))
fs.appendFileSync(outputFile, 'read -p "Press any key..."\n')

for (var nonce of [...Array(transfersPerWallet).keys()]) {
    for (var w of wallets) {
        const tx = signTx(networkCode, w.privateKey, composeTx(w.address, nonce + 1, genesisAddress, 1))
        fs.appendFileSync(outputFile, txToCommand(tx))
    }
}
