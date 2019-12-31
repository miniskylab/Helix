![logo](https://user-images.githubusercontent.com/26107905/71591947-df370080-2b60-11ea-8d62-d218aabd2331.png)

Helix is a free, Apache licensed website crawler. It systematically and recursively browses website using web browser to fully render each URL it visits and capture all the resources (images, videos, audios ...) which are downloaded in the rendering process. Helix is built using .NET Core framework and, right now, only supports Windows.

**Download**: *(coming soon)*  
**Demo video**: [https://youtu.be/0RWHfZj5sZA](https://www.youtube.com/watch?v=0RWHfZj5sZA)

# Screenshots

<span align="center">
  <img src="https://user-images.githubusercontent.com/26107905/71593434-97b37300-2b66-11ea-978d-285954e377f4.png" width="33%" />
  <img src="https://user-images.githubusercontent.com/26107905/71592949-adc03400-2b64-11ea-8d24-16ed16235245.png" width="32.9%" /> 
  <img src="https://user-images.githubusercontent.com/26107905/71592948-ad279d80-2b64-11ea-8d4f-2667d6d5a1e4.png" width="33%" />
</span>

![report](https://user-images.githubusercontent.com/26107905/71593486-e7923a00-2b66-11ea-94a1-3ee6b5e865f2.png)

# Use-cases

Helix can provide support in various scenarios:

 - **Estimate how big a website is**  
*Use Helix to scan the website then count the number of `internal URLs` in the report. That number will give you an estimate of how big a website is.*
 - **Locate broken URLs in a website**  
*Use Helix to scan the website then filter out URLs having status code greater than or equal to 400, or less than 0.*
 - **List out redirects**  
*Use Helix to scan the website then filter out 3xx URLs.*
 - **Estimate average page load time of a website**  
*Use Helix to scan the website then look at the `Avg. Page Load Time` number.*

# Features

 - Recursive and multi-threaded
 - Use web browser to fully render website
 - Capable of capturing all network traffic between the web browser and web server
	 - This feature allows Helix to capture every image loaded by a website. Even if they are used from `CSS`
 - Support `SQLite` and `CSV` outputs
 - Support using `SQL` to perform complex queries against `SQLite` output
 - Provide screenshot evidence of broken URLs
 - Graphical User Interface

# Usage

Just download the latest release of Helix and watch the demo video. Currently, Helix can only run on Windows.

**Download**: *(coming soon)*  
**Demo video**: [https://youtu.be/0RWHfZj5sZA](https://www.youtube.com/watch?v=0RWHfZj5sZA)
