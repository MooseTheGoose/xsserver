require "sinatra"

get "/" do
  <<~EOM
<html>
  <head>
     <titleHello, World!</title>
  </head>
  <body>
    <h1>Hello, World!</h1>
  </body>
</html>
EOM
end

