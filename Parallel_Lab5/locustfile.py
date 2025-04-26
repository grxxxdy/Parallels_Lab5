from locust import HttpUser, task


class User(HttpUser):

    @task
    def index_page(self):
        self.client.get("/index.html")

    @task
    def second_page(self):
        self.client.get("/page2.html")

    @task
    def error_page(self):
        self.client.get("/page123123.html")
