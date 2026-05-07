# Connecting a Zebra ZD421 series printer to Ethernet (the network)

Configure a fresh Zebra ZD421 printer to have a friendly IP address.  
**Be aware that changing the IP address of a printer that is already in use will remove access from any devices that are currently connected!**

## Setting the IP address

### Requirements for assigning IP address

- Printer
- USB-B cable (other side doesn't matter, just needs to be compatible with your computer and rated for data transfer. I used USB-A to USB-B.)
- Computer with admin privileges (required for driver install and setup program install/run)
  - The Zebra ZD421 printer driver can be downloaded from [the official Zebra website under the ZD421 support section](https://www.zebra.com/us/en/support-downloads/printers/desktop/zd421.html?downloadId=4fd677df-5ae1-4e2f-89ce-f33134dc1e70#Tab-item-61fee4a3fb-tab)
  - The Zebra Printer Setup Utility can be downloaded from [the official Zebra website on the Zebra Printer Setup Utilities page](https://www.zebra.com/us/en/support-downloads/software/printer-software/printer-setup-utilities.html?downloadId=3b7879b0-5037-4840-9d6a-b71b6f5c819a)

### Steps

This first part can be done regardless of whether computer/printer are connected to a central Ethernet hub.

#### Driver download walkthrough

Zebra is adamant that connecting the printer to your computer before instructed by the driver installation wizard will cause issues, but I did not find that to be the case.  
[Zebra's own article walking you through driver installation](https://support.zebra.com/article/000027281) is detailed enough, but a little confusing on the first read.

1. [Download the driver ZIP file](https://www.zebra.com/us/en/support-downloads/printers/desktop/zd421.html?downloadId=4fd677df-5ae1-4e2f-89ce-f33134dc1e70#Tab-item-61fee4a3fb-tab) (it should look like zddriver-v1062628275-certified.zip). Right click it and select "Extract All". When you open the extracted folder, you should see just one file: the executable to download the driver.
2. Run the exe file. You'll need to provide admin authorization to do this.
3. The default options all work fine, so just press "Next" a few times to accept their terms. You'll reach a screen that looks final, but is really just the beginning of the driver installation. Press "Finish" to move on.
4. Press "Next" to move on to the menu screen. From there, you want to select "Install Printer Driver". Accept some more terms on the next screen and press "Next".
5. You should now have a menu with detection options. Although the end goal is Ethernet, we have to select USB first because we don't have a LAN established yet.
6. If you haven't yet, connect the printer directly to the computer using the USB-B cable. It kind of looks square, but it's not, so make sure it's facing the right way on the printer side (should be flat side down).
    - The program should immediately detect the printer with its port information and ask you if it's the one you want.
      - If you happen to already have a version of this driver installed, it'll ask if you'd like to update it.
    - If not, try unplugging and replugging in the cable, because if this doesn't auto-detect, there's likely a problem with the computer or printer's physical USB port.

#### Zebra Setup Utilities installation

This program is worthless without a driver. If you haven't downloaded it yet, follow the steps in [the driver download walkthrough](#driver-download-walkthrough)
[Zebra has an article for ZSU install](https://support.zebra.com/article/000031410), but it beats around the bush for our purposes.

1. [Download the Zebra Setup Utilities ZIP file](https://www.zebra.com/us/en/support-downloads/software/printer-software/printer-setup-utilities.html?downloadId=3b7879b0-5037-4840-9d6a-b71b6f5c819a) (it should look like zsu-1191327.zip). Right click it and select "Extract All". When you open the extracted folder, you should see just one file: the executable to download the driver.
2. Run the exe file. You'll need to provide admin authorization to do this.
3. When the setup sequence is complete (shouldn't require any input), you should see a list of all the printers with drivers on the computer. Click the one you want to configure to select it, then press "Configure Printer Connectivity". This should open another window.
4. For Ethernet setup, select "Wired", then "Internal Print Server".
5. Select "Static" (instead of DCHP),then enter the desired IP address, subnet mask, and gateway. Hostname is optional.
    - The P in TCP and IP stand for protocol, so if you're not familiar with the protocol, here are some recommendations:
      - The first two "octets" (groups of three digits) of the IP address should be 192 and 168, respectively (for a format of 192.168.x.x)
      - The subnet mask used by Stanley is 255.255.248.0. This allows devices to communicate with sub-networks within 8 of its own, with some stipulations.
      - Leave the gateway blank, or set it manually to match the IP address (this is what the program will do for you if you leave it)
6. Press "Next", then "Finish" to send the change.
7. To quickly verify that the printer has saved its new IP address, select "Configure Printer Connectivity" from the ZSU main screen, then run `! U1 getvar "internal_wired.ip.addr"`. It should match the IP address you just set.

## Connecting over TCP

### Requirements for connecting to printer with known IP address

- Printer
- Computer with a built-in Ethernet port or a compatible Ethernet adapter
- Two Ethernet cables
- Central Ethernet hub (e.g. two ports to Stanley network or simple Ethernet switch that both devices may connect to).

1. If using an Ethernet switch, plug it in and power it on.
2. Use one Ethernet cable to connect the computer to a port in the central Ethernet hub, and the other Ethernet cable to connect the printer to a different port in the central Ethernet hub.
    - If you're using a good Ethernet switch, you should see some LEDs power on/flash intermittently to denote an accepted connection/data transfer.
3. Verify that the Ethernet connection on the computer side targets the same network as the printer.
    - The required specificity is directly tied to your subnet mask. If you used 255.255.248.0 as recommended, the first three octets *must* match, but the last *must* differ
    - To set/check this on Windows:
      1. Right-click the network icon in the lower right corner of your screen (next to the date, time, and volume) and select 'Open Network & Internet settings'
      2. In the 'Advanced network settings' section, select 'Change adapter options'. This opens a list of all network (e.g. Ethernet, WiFi, Bluetooth) connections your device has ever seen.
      3. Find the option that represents the Ethernet connection to the central Ethernet hub. It should be marked active. If you have multiple active Ethernet connections, be careful to choose the right one, or you might accidentally tamper with your Internet settings instead. If you're using an adapter, look for the adapter's name
      4. Right click that option and choose 'Properties'. It should look like it requires admin privileges, but it really just wants you to enter your password again.
      5. From the checklist of properties for this connection, find the one titled 'Internet Protocol Version 4 (TCP/IPv4)'. Click to highlight it, then press the Properties button that is now active.
      6. If you haven't used this Ethernet port/adapter on this computer, it probably has the item 'Obtain an IP address automatically' selected. Pick 'Use the following IP address' instead.
      7. Enter an open IP address on the same network, following the aforementioned rule about matching the network. I will reiterate: **This is a different IP address from the printer's!**
      8. Strictly speaking, the subnet mask doesn't *need* to match the printer's, but it's easier if it does.
      9. Enter the same default gateway as you did for the printer.
      10. Press OK to confirm the new IP settings, then again to update the property change.
4. If the printer is not on, turn it on now.
5. Go to the terminal (Command Prompt on Windows) and enter `ping <printer's_IP_address>` (without angle brackets, and with the printer's actual IP address).
    - If the network is configured correctly and you assigned the computer and printer good IP addresses on the network, you should see ping statistics like:

    ```cmd
    Pinging <printer's_IP_address> with 32 bytes of data:
    Reply from <printer's_IP_address>: bytes=32 time=2ms TTL=64
    Reply from <printer's_IP_address>: bytes=32 time=1ms TTL=64
    Reply from <printer's_IP_address>: bytes=32 time=1ms TTL=64
    Reply from <printer's_IP_address>: bytes=32 time=1ms TTL=64

    Ping statistics for <printer's_IP_address>:
        Packets: Sent = 4, Received = 4, Lost = 0 (0% loss),
    Approximate round trip times in milli-seconds:
        Minimum = 1ms, Maximum = 2ms, Average = 1ms
    ```

    - You'll know your printer and computer can't find each other if your ping statistics instead show:

    ```cmd
    Pinging <printer's_IP_address> with 32 bytes of data:
    Request timed out.
    Request timed out.
    Request timed out.
    Request timed out.
    ```
