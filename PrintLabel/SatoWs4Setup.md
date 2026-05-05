# Connecting a SATO WS4 series printer to Ethernet (the network)

Configure a fresh SATO WS408/412 printer to have a friendly IP address.  
**Be aware that changing the IP address of a printer that is already in use will remove access from any devices that are currently connected!**

## Setting the IP address

### Requirements for assigning IP address

- Printer
- USB-B cable (other side doesn't matter, just needs to be compatible with your computer and rated for data transfer. I used USB-A to USB-B.)
- Computer with SATO WS4 Printer Utility installed
  - The SATO WS4 Printer Utility requires admin rights to install, but not to run
  - It can be found on the P drive at P:/PE III/Manuals/SATO/SATO-WS4-Printer-Utility_V1.0.1.41.zip

### Steps

This first part can be done regardless of whether computer/printer are connected to a central Ethernet hub

1. Open the SATO WS4 Printer Utility on the computer and ensure the connection type dropdown in the upper left corner (just under File) says USB.
    - This is the default, so if it's your first time, it should already say this.
    - If the USB option isn't selected, this dropdown will say either COM or LAN.
2. Plug in the printer's power supply and turn it on (circular port & switch on the back).
3. Connect the printer directly to the computer using the USB-B cable. It kind of looks square, but it's not, so make sure it's facing the right way on the printer side (should be flat side down).
    - The program should immediately update the USB address in the upper bar to show something like `\\?\usb#vid` with a bunch of letters and numbers after that.
    - If not, try unplugging and replugging in the cable, because if this doesn't auto-detect, there's likely a problem with the computer or printer's physical USB port.
4. Use the sidebar on the left to navigate to the Parameter Setting page if not already there.
    - There should now be a bunch of tabs underneath the upper bar listing things like General, COM, LAN, etc.
5. Click the LAN tab.
6. On the LAN page, look for the 'Server' subsection (lower right) and select 'Disable' from the DCHP dropdown.
7. In the TCP/IP subsection (upper left), enter the desired IP address, subnet mask, and gateway.
    - The P in TCP and IP stand for protocol, so if you're not familiar with the protocol, here are some recommendations:
      - The first two "octets" (groups of three digits) of the IP address should be 192 and 168, respectively (for a format of 192.168.x.x, the private network standard)
      - The subnet mask used by Stanley is 255.255.248.0. This allows devices to communicate with sub-networks within 8 of its own, with some stipulations.
      - Leave the gateway as zeroes, or set it manually to match the IP address (this is what the program will do for you if you leave it)
8. Press the Send button in the blue bar between the tabs and the page contents.
    - The program will show a confirmation dialog telling you that changing the printer's IP address will sever its connection to any other devices. Confirm to enact the change.
9. If the success dialog tells you 'Done' without anything about an error, the IP address was successfully updated.
    - You should also see the printer's status lights briefly turn orange, then turn green again with a short print-like sound.
10. To quickly verify the new IP address/subnet mask, enter something different in the IP address/subnet mask fields, then press the Get button next to the Send button you used above. You should see another success dialog, and the IP address/subnet mask fields should be overwritten with whatever the printer's IP address actually is.

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
