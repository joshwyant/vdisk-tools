# vdisk-tools
Command-line tools for writing disk images, and a GUI for exploring them.

I made these tools in 2008 with a UI to create and modify disk images.

I used them for building my hobby operating system at the time (https://github.com/joshwyant/myos).

In 2018 (10 years later), I published the tools to github, converted the command-line tools to dotnet core,
and published a docker image (https://hub.docker.com/r/joshwyant/vdisk-tools/). 

The docker image doesn't contain the GUI.

Example usage (taken from myos):

```bash
echo creating disk image...
vmkimg -i hdd.img -M $DISKSIZE -b bin/bootsect -t $DISKTYPE>>/dev/zero

echo copying files...
vput hdd.img ./bin/osldr /osldr
vmkdir hdd.img /system/bin
vput hdd.img ./bin/kernel /system/bin/kernel
vput hdd.img ./bin/shell /system/bin/shell
vput hdd.img ./bin/vesadrvr.o /system/bin/vesadrvr.o

echo setting attributes...
vattr hdd.img /osldr rhs
vattr hdd.img /system/bin/kernel rs
vattr hdd.img /system/bin/shell rs
vattr hdd.img /system/bin/vesadrvr.o rs
```
