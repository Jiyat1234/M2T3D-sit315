#include <stdio.h>
#include <stdlib.h>
#include <pthread.h>
#include <semaphore.h>
#include <time.h>

#define BUFFER_SIZE_TRAFFIC 1
#define MAX_SIGNALS 10
#define NUM_PRODUCERS_TRAFFIC 2
#define NUM_CONSUMERS_TRAFFIC 2

typedef struct {
    time_t recordTime;
    int signalId;
    int vehiclesPassed;
} TrafficRecord;

typedef struct {
    TrafficRecord records[BUFFER_SIZE_TRAFFIC];
    int in, out;
    sem_t mutex, full, empty;
} TrafficBuffer;

TrafficBuffer trafficBuffer;
pthread_mutex_t congestionDataLock = PTHREAD_MUTEX_INITIALIZER;
int congestionData[MAX_SIGNALS];

time_t currentSystemTime; // Global variable to store current time

void initializeTrafficBuffer(TrafficBuffer* buffer) {
    buffer->in = 0;
    buffer->out = 0;
    sem_init(&buffer->mutex, 0, 1);
    sem_init(&buffer->full, 0, 0);
    sem_init(&buffer->empty, 0, BUFFER_SIZE_TRAFFIC);
}

void produceTrafficData(TrafficBuffer* buffer) {
    struct timespec currentTimeSpec;
    clock_gettime(CLOCK_REALTIME, &currentTimeSpec);

    for (int i = 0; i < 12; i++) {
        TrafficRecord newRecord = {
            .recordTime = currentTimeSpec.tv_sec + (i * 300), // 5 minutes interval
            .signalId = rand() % MAX_SIGNALS + 1,
            .vehiclesPassed = rand() % 100 + 1,
        };

        sem_wait(&buffer->empty);
        sem_wait(&buffer->mutex);

        buffer->records[buffer->in] = newRecord;
        buffer->in = (buffer->in + 1) % BUFFER_SIZE_TRAFFIC;

        sem_post(&buffer->mutex);
        sem_post(&buffer->full);

        usleep(rand() % 500000 + 500000); // Simulate delay between measurements
    }
}


void consumeTrafficData(TrafficBuffer* buffer) {
    while (1) {
        sem_wait(&buffer->full);
        sem_wait(&buffer->mutex);

        TrafficRecord record = buffer->records[buffer->out];
        buffer->out = (buffer->out + 1) % BUFFER_SIZE_TRAFFIC;

        sem_post(&buffer->mutex);
        sem_post(&buffer->empty);

        pthread_mutex_lock(&congestionDataLock);

        if (congestionData[record.signalId] != 0) {
            congestionData[record.signalId] += record.vehiclesPassed;
        } else {
            congestionData[record.signalId] = record.vehiclesPassed;
        }

        pthread_mutex_unlock(&congestionDataLock);

        char timestampStr[20];
        strftime(timestampStr, sizeof(timestampStr), "%Y-%m-%d %H:%M:%S", localtime(&record.recordTime));
        printf("Traffic Record - Time: %s, SignalId: %d, VehiclesPassed: %d\n", timestampStr, record.signalId, record.vehiclesPassed);

        usleep(100000); // Simulate processing time
    }
}

int main() {
    initializeTrafficBuffer(&trafficBuffer);

    pthread_t producerThreads[NUM_PRODUCERS_TRAFFIC];
    for (int i = 0; i < NUM_PRODUCERS_TRAFFIC; i++) {
        pthread_create(&producerThreads[i], NULL, (void*)&produceTrafficData, &trafficBuffer);
    }

    pthread_t consumerThreads[NUM_CONSUMERS_TRAFFIC];
    for (int i = 0; i < NUM_CONSUMERS_TRAFFIC; i++) {
        pthread_create(&consumerThreads[i], NULL, (void*)&consumeTrafficData, &trafficBuffer);
    }

    for (int i = 0; i < NUM_PRODUCERS_TRAFFIC; i++) {
        pthread_join(producerThreads[i], NULL);
    }

    pthread_mutex_lock(&congestionDataLock);
    // Notify consumers that no more data will be produced
    pthread_mutex_unlock(&congestionDataLock);

    for (int i = 0; i < NUM_CONSUMERS_TRAFFIC; i++) {
        pthread_join(consumerThreads[i], NULL);
    }

    return 0;
}

