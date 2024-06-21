# Makefile

# Compiler
CXX = g++

# Source file
SRC = UnlockCard.cpp

# Target executable
TARGET = UnlockCard

# Default rule
all: $(TARGET)

# Rule to create the target executable
$(TARGET): $(SRC)
	$(CXX) $(SRC) -o $(TARGET)

# Clean rule to remove the generated executable
clean:
	rm -f $(TARGET)
