^XA
^DFR:TEST.ZPL^FS ^FX Save to the R drive
^CI28 ^FX Switch to UTF-8 (ZPL default is Cp-850, which is archaic)

^FX for an 8x3 label at 8dpmm (203 dpi), the dimensions are 1624x609 ^FS
^FX for an 3x1 label at 8dpmm (203 dpi), the dimensions are 609x203 ^FS

^FX --------ROW 1--------
^LH3,3^FS ^FX Sets origin to not print at the edge
^FO0,0^GB603,197,3^FS ^FX Box to the safety edge of the label
^FO0,0^GB250,140^FS ^FX Dummy sample number container
^FO0,35^AF,90^FB250,1,0,C^FN1^FS ^FX Dummy sample number field
^FO250,0^GB1104,140^FS ^FX Model name container
^FO250,35^AF,80^FB1104,1,0,C^FN2^FS ^FX Model name field
^FO1354,0^GB250,140^FS ^FX Severity rank container
^FO1354,35^AF,90^FB250,1,0,C^FN3^FS ^FX Severity field

^FX --------ROW 2--------
^LH10,150^FS ^FX Sets origin to start at row 2 (standardizes y height row-wide)
^FO0,0^GB70,100,70^FS ^FX Excluding smaller dimension creates filled box (for ID title)
^FO0,30^A0,50^FB70,1,0,C^FR^FDID\&^FS ^FX Centered text in filled box
^FO70,0^GB180,100^FS ^FX Dummy sample serial container
^FO70,25^AF,50^FB180,1,0,C^FN4^FS ^FX Dummy sample serial field
^FO250,0^GB552,100^FS ^FX Assembly line container
^FO250,25^AF,50^FB552,1,0,C^FN5^FS ^FX Assembly line field
^FO802,0^GB161,,100^FS ^FX Filled box (for REV title)
^FO802,30^A0,50^FB161,1,0,C^FR^FDREV\&^FS ^FX Centered text in filled box
^FO963,0^GB241,100^FS ^FX Iteration number container
^FO963,20^AF,70^FB241,1,0,C^FN6^FS ^FX Iteration number field
^FO1204,0^GB400,100^FS ^FX Creation date container
^FO1204,25^AF,45^FB400,1,0,C^FN7^FS ^FX Creation date field

^FX --------ROW 3--------
^LH10,250^FS ^FX Sets origin to start at row 3 (standardizes y height row-wide)
^FO0,0^GB1204,249^FS ^FX Process failure mode container
^FO0,25^AF,50^FB1204,2,0,C^FN8^FS ^FX Process failure mode field
^FO1204,0^GB400,249^FS ^FX Location container
^FO1204,25^AF,50^FB400,3,0,C^FN9^FS ^FX Location field

^FX --------ROW 4--------
^LH10,499^FS ^FX Sets origin to start at row 4 (standardizes y height row-wide)
^FO0,0^GB240,,100^FS ^FX Filled box (for MAKER title)
^FO0,30^A0,50^FB240,1,0,C^FR^FDMAKER\&^FS ^FX Centered text in filled box
^FO240,0^GB562,100^FS ^FX Creator container
^FO240,20^AF,50^FB562,1,0,C^FN10^FS ^FX Creator field
^FO802,0^GB260,,100^FS ^FX Filled box (for APPROVAL title)
^FO802,30^A0,50^FB260,1,0,C^FR^FDAPPROVAL\&^FS ^FX Centered text in filled box
^FO1062,0^GB542,100^FS ^FX Approver container
^FO1062,20^AF,50^FB542,1,0,C^FN11^FS ^FX Approver field

^XZ

^XA
^XFR:TEST.ZPL ^FX Load the template
^FN1^FD1\&^FS
^FN2^FDT0G F-HCU\&^FS
^FN3^FDG\&^FS
^FN4^FD3878\&^FS
^FN5^FDEL2F3\&^FS
^FN6^FD1\&^FS
^FN7^FD02/27/2014^FS
^FN8^FDA410 NG ENCODER (C-TYPE)^FS
^FN9^FDIN CIRCUIT TEST MC\&^FS
^FN10^FDB. NEELY\&^FS
^XZ
