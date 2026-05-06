^XA
^CI28 ^FX Switch to UTF-8 for character encoding (ZPL default is Cp-850, which isn't friendly for streaming via TcpClient)

^FX for an 3x1 label at 12dpmm (304.8 dpi, colloquially 300 dpi), the dimensions are 914x304 ^FS

^FX --------ROW 1--------
^LH15,15^FS ^FX Sets origin to not print at the edge
^FO0,0^GB884,282,4^FS ^FX Box to the safety edge of the label
^FO0,0^GB120,60^FS ^FX Dummy sample number container (comfortably fits 4 digits)
^FO0,20^AF,26^FB120,,,C^FD{0}\&^FS ^FX Dummy sample number field
^FO120,0^GB692,60^FS ^FX Model name container (Space for 28/32 characters)
^FO120,20^AF,26^FB692,,,C^FD{1}\&^FS ^FX Model name field
^FO812,0^GB72,60^FS ^FX Severity rank container (Plenty of space for 1 character)
^FO812,20^AF,26^FB72,,,C^FD{2}\&^FS ^FX Severity field

^FX --------ROW 2--------
^LH15,75^FS ^FX Sets origin to start at row 2 (standardizes y height row-wide)
^FO0,0^GB39,49,39^FS ^FX Filled box (for ID title)
^FO0,15^A0,24^FB39,,,C^FR^FDID\&^FS ^FX Centered text in filled box
^FO39,0^GB192,49^FS ^FX Dummy sample serial container (Space for 7/10 digits)
^FO39,12^AF,26^FB192,,,C^FD{3}\&^FS ^FX Dummy sample serial field
^FO231,0^GB234,49^FS ^FX Assembly line container (Space for 9 characters, 2 more than current max)
^FO231,12^AF,26^FB234,,,C^FD{4}\&^FS ^FX Assembly line field
^FO464,0^GB60,,49^FS ^FX Filled box (for REV title)
^FO464,15^A0,24^FB60,,,C^FR^FDREV\&^FS ^FX Centered text in filled box
^FO524,0^GB112,49^FS ^FX Iteration number container (Space for 4 digits, 2 more than current max)
^FO524,12^AF,26^FB112,,,C^FD{5}\&^FS ^FX Iteration number field
^FO636,0^GB248,49^FS ^FX Creation date container (Fits full date with space)
^FO636,12^AF,26^FB248,,,C^FD{6}\&^FS ^FX Creation date field

^FX --------ROW 3--------
^LH15,122^FS ^FX Sets origin to start at row 3 (standardizes y height row-wide)
^FO0,0^GB665,126^FS ^FX Process failure mode container (space for all 100 characters)
^FO0,12^AF,28^FB665,4,7,C^FD{7}\&^FS ^FX Process failure mode field
^FO665,0^GB219,126^FS ^FX Location container (space for all 32 characters)
^FO665,12^AF,28^FB219,4,7,C^FD{8}\&^FS ^FX Location field

^FX --------ROW 4--------
^LH15,248^FS ^FX Sets origin to start at row 4 (standardizes y height row-wide)
^FO0,0^GB113,,49^FS ^FX Filled box (for MAKER title)
^FO0,15^A0,24^FB113,,,C^FR^FDMAKER\&^FS ^FX Centered text in filled box
^FO113,0^GB316,49^FS ^FX Creator container (plenty of space for associate number)
^FO113,10^AF,28^FB316,,,C^FD{9}\&^FS ^FX Creator associate number field
^FO429,0^GB145,,49^FS ^FX Filled box (for APPROVAL title)
^FO429,15^A0,24^FB145,,,C^FR^FDAPPROVAL\&^FS ^FX Centered text in filled box
^FO574,0^GB310,49^FS ^FX Approver container (a little small for in-person signature, but not much to be done)

^XZ
^FO596,7^AF,28^FB310,,,C^FD[10]\&^FS ^FX Approver field (likely never used, unless Stanley moves away from physically signing label)
^FX Swap out square brackets for curly braces if this is used
