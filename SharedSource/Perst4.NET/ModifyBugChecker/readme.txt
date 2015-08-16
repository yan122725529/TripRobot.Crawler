.Net reflection mechanism doesn't allow to detect and handle access to an object fields (behavioral reflection).
As far as Perst4.Net is not using some special byte code preprocessors, compilers or virtual machines,
it is responsibility of programmer to save updated objects (Persistent.Store method) or 
mark modified objects (Persistent.Modify). Unfortunately this is one of the most error prone places
with Perst API - there is no way to check it in runtime if object was properly mark as modified
and if programmer forgot to do it, then behavior of the program may be unpredictable. 
This bug is very hard to reproduce, detect and fix.

This is why special utility is provided which is able to check in .Net assemblies that 
persistent objects are properly modified. This utility is add-in for 
Reflector ("http://www.red-gate.com/products/reflector/") which is included in Perst4.NET
distributive.

To use this utility start Reflector from Perst4.NET\ModifyBugChecker\Reflector directory.
In "View/Add-ins..." menu choose "Add" and open "Perst4.NET\ModifyBugChecker\PerstAddin\bin\Debug\PerstAddin.dll".
After it in "Tools" you should have "Detect Modify Bug" command. Now you can open ("File/Open")
your .Net application or library and run this command. Reflector will output message with the
list of all detected bugs and also save this text in the file "bug.lst" in the current directory.
Sorry, but reflect doesn't provide information about file name and line number for IL code.
So error message just includes name of the field or variable which reference updated object
and class+method name where this object is updated without proper call of Modify() or Store() method.

Please notice that this utility requires that Modify() method should be always invokes
AFTER update of the object fields. In some cases this requirement is not so critical.
For example, both following code fragments are correct:

      foo.x = 1;
      foo.Modify();

and
 
      foo.Modify();
      foo.x = 1;

But in some cases updated objects may be forced to be stored in the database. 
For example it happens if newly created object is inserted in some index.
If you call Modify() before insertion and then continue to update the object, 
then this modifications may be lost since dirty bit is already cleared.
This is why it is highly recommended to call Modify() always after update
(obviously Store() must be called after update).

This add-in is not able to perform full data flow analysis, so sometimes it is not able to 
conclude that there is no execution path where object is updated but Modify() is not invoked.
Also it doesn't verify the code of constructors, assuming that constructor is initializing objects
which may be later stored in the database if there is reference to this object from some other 
persistent object. But there may be a bug not detected by this add-in if constructor inserts object
in some index and then continue its initialization. 
Although Perst4.NET doesn't require any more for persistent capable classes to be derived 
from Persistent base class, this add-in is able to perform analysis
only for such classes - all updated of fields of the objects which classes are not derived
from Persistent are ignored.


