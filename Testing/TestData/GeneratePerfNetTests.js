const fs = require('fs')
const chainiumSdk = require('chainium-sdk/src/index')

////////////////////////////////////////////////////////////////////////////////////////////////////
// Test Parameters
////////////////////////////////////////////////////////////////////////////////////////////////////

const networkCode = 'OWN_PUBLIC_BLOCKCHAIN_PERFNET'
const genesisPrivateKey = '5TYZaZhabnWPhqe7Tfy7ziY61JZZQH24nbf6cRhYDL6W'
const genesisAddress = chainiumSdk.crypto.addressFromPrivateKey(genesisPrivateKey)

const nodeCount = 4
const initialBalance = 10000
const txsPerNode = 1000
const actionsPerTx = 1
const actionFee = 0.001

const outputDir = './Output'
const preparationScriptFile = `${outputDir}/perfnet_test_prepare.sh`
const nodeScriptFile = `${outputDir}/perfnet_test_run.sh`

////////////////////////////////////////////////////////////////////////////////////////////////////
// Functions
////////////////////////////////////////////////////////////////////////////////////////////////////

function composePreparationTx(addresses) {
    const actions = []

    addresses.forEach(a => {
        actions.push({
            ActionType: 'TransferChx',
            ActionData: {
                RecipientAddress: a,
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

function composeTx(senderAddress, nonce, recipientAddresses, amount) {
    const tx = {
        SenderAddress: senderAddress,
        Nonce: nonce,
        ActionFee: actionFee,
        Actions: []
    }

    Array(actionsPerTx).fill().forEach((_, index) => {
        tx.Actions.push(
            {
                ActionType: 'TransferChx',
                ActionData: {
                    RecipientAddress: recipientAddresses[index % recipientAddresses.length],
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

function txToCommand(tx) {
    return `curl -H "Content-Type: application/json" -d @- http://localhost:10717/tx << JSON\n${tx}\nJSON\n`
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Compose
////////////////////////////////////////////////////////////////////////////////////////////////////

if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir)
}

const senderWallets = Array(nodeCount).fill().map(_ => chainiumSdk.crypto.generateWallet())

const initialTx = signTx(
    networkCode,
    genesisPrivateKey,
    composePreparationTx(senderWallets.map(w => w.address)))

fs.writeFileSync(preparationScriptFile, txToCommand(initialTx))
fs.chmodSync(preparationScriptFile, '777');

let nodeNumber = 0
for (const w of senderWallets) {
    const runScriptFile = nodeScriptFile + `.${(++nodeNumber).toString().padStart(3, '0')}`
    fs.writeFileSync(runScriptFile, `# Node: ${nodeNumber} / Sender: ${w.address}\n`)
    fs.chmodSync(runScriptFile, '777');

    for (let nonce = 1; nonce <= txsPerNode; nonce++) {
        const recipientAddresses = [genesisAddress]
        const tx = signTx(networkCode, w.privateKey, composeTx(w.address, nonce, recipientAddresses, 1))
        fs.appendFileSync(runScriptFile, txToCommand(tx))
    }
}
