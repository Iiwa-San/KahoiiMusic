FROM node:20-slim

# Install python3 and yt-dlp for reliable YouTube audio stream resolution
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 \
    ca-certificates \
    curl \
    && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY package*.json ./
RUN npm install

COPY tsconfig.json ./
COPY src ./src
RUN npm run build

ENV PORT=7860
EXPOSE 7860

CMD ["npm", "start"]
