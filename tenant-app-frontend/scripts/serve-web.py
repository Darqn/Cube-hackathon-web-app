"""Serve the Expo SPA locally, including direct navigation to nested routes."""
import http.server, pathlib, sys, urllib.parse
root = pathlib.Path(sys.argv[1]).resolve()
class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs): super().__init__(*args, directory=str(root), **kwargs)
    def do_GET(self):
        path = urllib.parse.urlparse(self.path).path
        if not (root / path.lstrip('/')).is_file() and not pathlib.PurePosixPath(path).suffix:
            self.path = '/index.html'
        return super().do_GET()
http.server.ThreadingHTTPServer(('127.0.0.1', int(sys.argv[2]) if len(sys.argv)>2 else 8082), Handler).serve_forever()
