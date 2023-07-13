# Design philosophy

In the current version of the application, and when possible, drivers are explicitly bound to hardware vendor and/or product IDs.
These mappings are currently defined as metadata in the code, which makes updating said mappings more difficult for users.
While we could somewhat alleviate the problem by storing mappings separately in configuration files and allowing the users to tweak them, it would likely expose users to more risky manipulations, as they could inadvertently enable a wrong driver on their system.
Forcing knowledgeable users to update the code and rebuild the application to test things on their side, seems like a better long-term scenario. The main drawback being, that at first, and upon release of newer already-supported hardware, the application will need one or many updates to enable support everything. But this minor inconvenience is still better that breaking many installations because of an incomaptible driver being accidentally enabled on newer exotic hardware.

The better way to address this is to make sure that minor updates to the application can be released very quickly, by having a robust and efficient CD pipeline.
