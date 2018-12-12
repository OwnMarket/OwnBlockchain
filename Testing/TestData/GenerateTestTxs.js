const fs = require('fs')
const chainiumSdk = require('chainium-sdk/src/index')

////////////////////////////////////////////////////////////////////////////////////////////////////
// Test Parameters
////////////////////////////////////////////////////////////////////////////////////////////////////

const genesisPrivateKey = "1EQKWYpFtKZ1rMTqAH8CSLVjE5TN1nPpofzWF68io1HPV"
const genesisAddress = "CHQcJKysWbbqyRm5ho44jexA8radTZzNQQ2"

const validatorAddresses =
    [
        "CHMf4inrS8hnPNEgJVZPRHFhsDPCHSHZfAJ",
        "CHXr1u8DvLmRrnBpVmPcEH43qBhjezuRRtq",
        "CHN5FmdEhjKHynhdbzXxsNB35oxL5195XE5",
        "CHStDQ5ZFeFW9rbMhw83f7FXg19okxQD9E7"
    ]

const validatorStake = 500000
const initialBalance = 10000
const walletCount = 100
const transfersPerWallet = 10
const fee = 0.001

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
        Fee: fee,
        Actions: actions
    }
}

function composeTx(senderAddress, nonce, recipientAddress, amount) {
    return {
        SenderAddress: senderAddress,
        Nonce: nonce,
        Fee: fee,
        Actions: [
            {
                ActionType: "TransferChx",
                ActionData: {
                    RecipientAddress: recipientAddress,
                    Amount: amount
                }
            }
        ]
    }
}

function signTx(privateKey, tx){
    const txRaw = chainiumSdk.crypto.utf8ToHex(JSON.stringify(tx));
    const txBase64 = chainiumSdk.crypto.encode64(txRaw);
    const signature = chainiumSdk.crypto.signMessage(privateKey, txRaw);

    return JSON.stringify({
        tx: txBase64,
        signature: signature
    })
}

let invocation = 0
function txToCommand(tx) {
    invocation++
    let port = 10701 + (invocation % 4)
    return `curl -H "Content-Type: application/json" -d ${JSON.stringify(tx)} http://localhost:${port}/tx\n`
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Compose
////////////////////////////////////////////////////////////////////////////////////////////////////

const wallets = [...Array(walletCount).keys()].map(() => chainiumSdk.crypto.generateWallet())

const initialTx = signTx(genesisPrivateKey, composeInitialTx(validatorAddresses, wallets.map(w => w.address)))
fs.writeFileSync(outputFile, txToCommand(initialTx))
fs.appendFileSync(outputFile, 'read -p "Press any key..."\n')

for (var nonce of [...Array(transfersPerWallet).keys()]) {
    for (var w of wallets) {
        const tx = signTx(w.privateKey, composeTx(w.address, nonce + 1, genesisAddress, 1))
        fs.appendFileSync(outputFile, txToCommand(tx))
    }
}
