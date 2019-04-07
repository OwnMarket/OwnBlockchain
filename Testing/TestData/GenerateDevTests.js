const fs = require('fs')
const chainiumSdk = require('chainium-sdk/src/index')

////////////////////////////////////////////////////////////////////////////////////////////////////
// Test Parameters
////////////////////////////////////////////////////////////////////////////////////////////////////

const networkCode = "OWN_PUBLIC_BLOCKCHAIN_DEVNET"
const genesisPrivateKey = "ZXXkM41yHhkzb2k5KjeWuGCzYj7AXAfJdMXqKM4TGKq"
const genesisAddress = chainiumSdk.crypto.addressFromPrivateKey(genesisPrivateKey)

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
const txsPerWallet = 10
const actionsPerTx = 1
const actionFee = 0.001

const outputDir = './Output'
const outputFile = `${outputDir}/dev_test_run.sh`

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

function composeTx(senderAddress, nonce, wallets, amount) {
    const tx = {
        SenderAddress: senderAddress,
        Nonce: nonce,
        ActionFee: actionFee,
        Actions: []
    }

    const recipients = wallets.filter(w => w.address !== senderAddress)

    Array(actionsPerTx).fill().forEach((_, index) => {
        const recipientAddress = recipients[index % recipients.length].address
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
    const txRaw = chainiumSdk.crypto.utf8ToHex(JSON.stringify(tx))
    const txBase64 = chainiumSdk.crypto.encode64(txRaw)
    const signature = chainiumSdk.crypto.signMessage(networkCode, privateKey, txRaw)

    return JSON.stringify({
        tx: txBase64,
        signature: signature
    })
}

let invocation = 0
function txToCommand(tx) {
    invocation++
    const port = 10701 + (invocation % 4)
    //return `curl -s -H "Content-Type: application/json" -d ${JSON.stringify(tx)} http://localhost:${port}/tx\n`
    return `curl -s -H "Content-Type: application/json" -d @- http://localhost:${port}/tx << JSON\n${tx}\nJSON\n`
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Compose
////////////////////////////////////////////////////////////////////////////////////////////////////

if (!fs.existsSync(outputDir)){
    fs.mkdirSync(outputDir)
}

const wallets = Array(walletCount).fill().map(_ => chainiumSdk.crypto.generateWallet())

const initialTx = signTx(
    networkCode,
    genesisPrivateKey,
    composeInitialTx(validatorAddresses, wallets.map(w => w.address)))

fs.writeFileSync(outputFile, txToCommand(initialTx))
fs.chmodSync(outputFile, '777');
fs.appendFileSync(outputFile, 'read -p "Press any key..."\n')

for (const nonce of [...Array(txsPerWallet).keys()]) {
    for (const w of wallets) {
        const tx = signTx(networkCode, w.privateKey, composeTx(w.address, nonce + 1, wallets, 1))
        fs.appendFileSync(outputFile, txToCommand(tx))
    }
}
