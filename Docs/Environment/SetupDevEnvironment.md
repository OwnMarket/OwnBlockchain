# DEV Environment Setup


## VM Preparation

- Install [VirtualBox](https://www.virtualbox.org)

- Create new virtual machine with following settings:
    - Name: `Own_DEV` (or any other)
    - Type: `Linux`
    - Version: `Ubuntu (64 bit)`
    - At least `2048 MB` RAM
    - At least `30 GB` HDD (VDI, dynamically allocated)
    - Settings:
        - General > Advanced:
            - Shared Clipboard: `Bidirectional`
            - Drag'n'Drop: `Bidirectional`
        - System > Processor:
            - Processor(s): `2` (at least)
        - Display:
            - Activate `Enable 3D Acceleration` option (can improve VM responsiveness)
        - Network > Adapter 1:
            - Attached To: `NAT`
        - Network > Adapter 2:
            - Attached To: `Host-only Adapter`

- Install [Ubuntu 16.04.x LTS](https://www.ubuntu.com/download/desktop) on the virtual machine. If you experience performance issues, you can install [Xubuntu](https://xubuntu.org) instead, which is less resource intensive.
    - Username: `developer`
    - Machine name: `own`

- Shut down the VM
- Create a VM snapshot and name it `OS installed`.
- Start the VM

- Install _VirtualBox Guest Additions_
    - On running VM window go to _Devices > Insert Guest Additions CD Image_
        - if the installation doesn't start automatically, you can start it manually by running `sudo /media/developer/VBOXADDITIONS_X.X.XX_XXXXXX/autorun.sh` (replace X.X.XX_XXXXXX with real version number of your VirtualBox installation)
        - if CD installation does not work, alternatively you can install it by running `sudo apt-get install virtualbox-guest-utils`
    - Follow the instructions inside the VM
    - Restart the VM
    - Eject the Guest Additions CD Image after restart

- Shut down VM
- Create a VM snapshot and name it `Guest Additions installed`.
- Start the VM


## Frameworks and Tools Setup

- Setup development environment in VM by executing following command in VMs terminal:

```
wget -O - https://raw.githubusercontent.com/OwnMarket/OwnBlockchain/master/Docs/Environment/setup_dev_environment.sh | bash
```
